using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Games;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Manager moves a Team to another Division mid-Stage (#10; see CONTEXT.md,
/// Division): the exceptional movement, say to rebalance after a Withdrawal
/// (#21), by its own code path, not Relegation's. The Team starts at the
/// bottom of its new Division and plays everyone there; its Games in the
/// Division it leaves are dropped, none of them having a result.
/// </summary>
/// <remarks>
/// <para>
/// A Team that already has a Score this Stage can't move (owner's to confirm,
/// 2026-11-22): every result stays where it was played, and the Teams left
/// behind are ranked alike, whether or not they had played it. It moves at
/// the close instead. Its Games marked not played count for nothing, and go.
/// </para>
/// <list type="bullet">
/// <item>Managers only, by the routing table's policy. No League, no Stage, or no such Team in it: 404.</item>
/// <item>The League has ended: 409 <c>stage-closed</c>. A Team off the ladder: 409 <c>not-on-the-ladder</c>.</item>
/// <item>A Foursomes League (#14): 409 <c>foursomes-fixed</c>. Its Divisions are always 4, and a Player's
/// Games are a rotation of the four, so one Player can't move alone.</item>
/// <item>A Score this Stage: 409 <c>team-has-results</c>. Leaving a Division of two: 409 <c>division-too-small</c>,
/// since a Division keeps at least two Teams.</item>
/// <item>A Division the ladder hasn't got, or the one it's in: 400 under <c>toDivision</c>.</item>
/// </list>
/// It holds the League's entry and results locks, as closing does, so no Score
/// or close lands in the middle of it.
/// </remarks>
public static class MoveTeam
{
    public static async Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
        MoveTeamRequest request,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        await using var transaction = await LeagueEntry.LockLeagueAsync(db, leagueId, cancellationToken);
        await GameScores.TakeResultsLockAsync(db, leagueId, cancellationToken);
        var stage = await db.Stages
            .Where(s => s.LeagueId == leagueId)
            .OrderByDescending(s => s.Number)
            .Select(s => new { s.Id, Closed = s.ClosedAt != null })
            .FirstOrDefaultAsync(cancellationToken);
        if (stage is null || !await db.Teams.AnyAsync(t => t.LeagueId == leagueId && t.Id == request.TeamId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        if (stage.Closed)
        {
            return StageClose.AlreadyClosed();
        }

        if (await db.Leagues.AnyAsync(l => l.Id == leagueId && l.Type == LeagueType.Foursomes, cancellationToken))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "foursomes-fixed",
                title: "A Foursomes Player can't move Division mid-Stage.",
                detail: "A Foursomes Division is always 4 Players, who play their 3 Games together. Players move at the close.");
        }

        var places = await db.LadderPlaces
            .Where(p => p.StageId == stage.Id)
            .OrderBy(p => p.Position)
            .Select(p => new { p.TeamId, p.Division })
            .ToListAsync(cancellationToken);
        if (places.SingleOrDefault(p => p.TeamId == request.TeamId) is not { } place)
        {
            return LeagueLadder.NotOnTheLadder();
        }

        var divisions = places.Max(p => p.Division);
        if (request.ToDivision > divisions || request.ToDivision == place.Division)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["toDivision"] = [request.ToDivision == place.Division
                    ? $"It's in Division {place.Division} already."
                    : $"This ladder has {divisions} Divisions."],
            });
        }

        if (await db.Games.AnyAsync(
                g => g.StageId == stage.Id && g.Score != null && g.Sides.Any(s => s.TeamId == request.TeamId), cancellationToken))
        {
            return LeagueLadder.HasResults();
        }

        if (places.Count(p => p.Division == place.Division) <= League.MinDivisionSize)
        {
            return LeagueLadder.DivisionTooSmall(place.Division);
        }

        // Its Games in the Division it leaves: none has a Score, so no result or history goes with them.
        await db.Games
            .Where(g => g.StageId == stage.Id && g.Score == null && g.Sides.Any(s => s.TeamId == request.TeamId))
            .ExecuteDeleteAsync(cancellationToken);

        // The ladder again, the Team last in its new Division, and every place renumbered: a Division is a run of
        // consecutive places (ADR-0030), and a place's key is its position.
        var opponents = places.Where(p => p.Division == request.ToDivision).Select(p => p.TeamId).ToList();
        var order = places
            .Where(p => p.TeamId != request.TeamId)
            .Select(p => (p.TeamId, p.Division))
            .ToList();
        order.Insert(order.FindLastIndex(p => p.Division == request.ToDivision) + 1, (request.TeamId, request.ToDivision));
        await db.LadderPlaces.Where(p => p.StageId == stage.Id).ExecuteDeleteAsync(cancellationToken);
        for (var index = 0; index < order.Count; index++)
        {
            db.LadderPlaces.Add(new LadderPlace
            {
                StageId = stage.Id,
                LeagueId = leagueId,
                Position = index + 1,
                Division = order[index].Division,
                TeamId = order[index].TeamId,
            });
        }

        // It plays everyone in its new Division once; they're higher, so each is Side 1.
        foreach (var opponent in opponents)
        {
            var game = new Game { Id = Guid.NewGuid(), LeagueId = leagueId, StageId = stage.Id, Division = request.ToDivision };
            game.Sides.Add(new GameSide { LeagueId = leagueId, Side = 1, TeamId = opponent });
            game.Sides.Add(new GameSide { LeagueId = leagueId, Side = 2, TeamId = request.TeamId });
            db.Games.Add(game);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
