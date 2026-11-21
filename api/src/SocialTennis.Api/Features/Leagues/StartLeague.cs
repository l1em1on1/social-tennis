using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Manager starts a Divisional League (#7) with the ladder they confirmed:
/// Stage 1 opens (<see cref="StageDates.First"/>), each Team takes its place
/// in that order, in the Divisions the Manager sized, and every pair in a
/// Division gets a Game (ADR-0031). There is no Bye. Teams left out stay in
/// the League without a place. Doubles Players still without a
/// partner are removed, with their approved Join Requests, so they may ask
/// again (owner, 2026-11-20).
/// </summary>
/// <remarks>
/// <para>
/// The request says what the Manager saw happen to everyone: every Team placed
/// or left out, and every Player removed. If that no longer matches the League
/// (a Team was paired, or someone added, since the Start screen loaded), it's
/// a 409 <c>league-changed</c> and nothing happens, so nobody is left out or
/// removed unseen.
/// </para>
/// <para>
/// A League starts once: a second start is a 409 <c>already-started</c>. The
/// League's lock serialises two at once, and the unique Stage number backs it.
/// Knockout (#12) and Foursomes (#14) Leagues can't start yet.
/// </para>
/// </remarks>
public static class StartLeague
{
    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
        StartLeagueRequest request,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        await using var transaction = await LeagueEntry.LockLeagueAsync(db, leagueId, cancellationToken);
        var league = await db.Leagues
            .Where(l => l.Id == leagueId)
            .Select(l => new { l.Type, l.Structure, l.StartDate, l.EndDate, l.StageLength })
            .SingleOrDefaultAsync(cancellationToken);
        if (league is null)
        {
            return TypedResults.NotFound();
        }

        if (await LeagueLadder.CannotStartAsync(db, leagueId, league.Structure, league.Type, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var teams = await db.Teams.Where(t => t.LeagueId == leagueId).Select(t => t.Id).ToListAsync(cancellationToken);
        var unpaired = await LeagueLadder.UnpairedAsync(db, leagueId, league.Type, cancellationToken);
        if (!teams.ToHashSet().SetEquals(request.Ladder.Concat(request.LeftOut))
            || !unpaired.Select(p => p.PlayerId).ToHashSet().SetEquals(request.Removed))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "league-changed",
                title: "The League has changed since the start was proposed.",
                detail: "Someone was added, paired or removed in the meantime. Look at the proposed ladder again.");
        }

        foreach (var playerId in request.Removed)
        {
            await LeagueEntry.RemoveAsync(db, leagueId, playerId, cancellationToken);
        }

        var (start, end) = StageDates.First(league.StartDate, league.EndDate, league.StageLength);
        var stage = new Stage
        {
            Id = Guid.NewGuid(),
            LeagueId = leagueId,
            Number = 1,
            StartDate = start,
            EndDate = end,
            OpenedAt = clock.GetUtcNow(),
        };
        db.Stages.Add(stage);
        // Each Division in turn down the ladder, at the sizes the Manager
        // confirmed, and every pair in it plays once, the higher place as Side 1.
        var position = 0;
        for (var division = 1; division <= request.Divisions.Count; division++)
        {
            var members = request.Ladder.Skip(position).Take(request.Divisions[division - 1]).ToList();
            foreach (var teamId in members)
            {
                stage.Places.Add(new LadderPlace { LeagueId = leagueId, Position = ++position, Division = division, TeamId = teamId });
            }

            foreach (var (first, second) in Ladder.RoundRobin(members.Count))
            {
                var game = new Game { Id = Guid.NewGuid(), LeagueId = leagueId, Division = division };
                game.Sides.Add(new GameSide { LeagueId = leagueId, Side = 1, TeamId = members[first] });
                game.Sides.Add(new GameSide { LeagueId = leagueId, Side = 2, TeamId = members[second] });
                stage.Games.Add(game);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
