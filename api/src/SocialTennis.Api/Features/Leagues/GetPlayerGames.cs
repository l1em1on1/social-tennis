using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Contracts;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Player's Games this Stage, by League (#42): each League they're in whose
/// current Stage is open, with its deadline, their Division and their Games
/// with any Score, and whether the Stage is held up and on what. Home shows
/// it; it reads each ladder as the ladder does (<see cref="StageLadder"/>).
/// </summary>
/// <remarks>
/// The holder's only: another User's Player is a 404, as for their Leagues
/// (<see cref="GetPlayerLeagues"/>). A Player removed from a Club, or who left
/// it, no longer sees its Leagues (#109). A League not started, or ended, has
/// nothing owed, so it isn't listed.
/// </remarks>
public static class GetPlayerGames
{
    public static async Task<Results<Ok<GetPlayerGamesResponse>, NotFound>> HandleAsync(
        Guid playerId,
        ClaimsPrincipal user,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        if (!await db.Players.AnyAsync(p => p.Id == playerId && p.UserId == userId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var leagues = await db.LeagueParticipants
            .Where(p => p.PlayerId == playerId
                && db.ClubMemberships.Any(m => m.ClubId == p.League.ClubId && m.PlayerId == playerId && m.Status == ClubMembershipStatus.Active))
            .Select(p => new
            {
                p.LeagueId,
                p.TeamId,
                p.League.Name,
                ClubName = p.League.Club.Name,
                p.League.Club.TimeZone,
                p.League.Type,
                p.League.ScoreFormat,
                p.League.StandingsMode,
                Stage = p.League.Stages
                    .OrderByDescending(s => s.Number)
                    .Select(s => new { s.Id, s.Number, s.StartDate, s.EndDate, Closed = s.ClosedAt != null })
                    .FirstOrDefault(),
            })
            .Where(l => l.Stage != null && !l.Stage.Closed)
            .ToListAsync(cancellationToken);

        var items = new List<PlayerGamesLeague>();
        foreach (var league in leagues.OrderBy(l => l.Stage!.EndDate).ThenBy(l => l.Name))
        {
            var stage = league.Stage!;
            var (divisions, awaiting) = await StageLadder.BuildAsync(db, stage.Id, league.ScoreFormat, league.Type, league.StandingsMode, cancellationToken);
            var division = league.TeamId is { } teamId
                ? divisions.FirstOrDefault(d => d.Competitors.Any(c => c.TeamId == teamId))
                : null;
            var games = division is null
                ? []
                : division.Games
                    .Select(g => (Game: g, First: SideOf(g.FirstTeamId, g.FirstPartnerTeamId), Second: SideOf(g.SecondTeamId, g.SecondPartnerTeamId)))
                    .Where(g => g.First.Contains(league.TeamId!.Value) || g.Second.Contains(league.TeamId!.Value))
                    .Select(g =>
                    {
                        // A Side is one Team, or in Foursomes two Players' Teams of one (ADR-0036): partners are the rest of yours.
                        var yourSide = g.First.Contains(league.TeamId!.Value) ? 1 : 2;
                        var (ours, theirs) = yourSide == 1 ? (g.First, g.Second) : (g.Second, g.First);
                        var players = (IEnumerable<Guid> teams) =>
                            teams.SelectMany(team => division.Competitors.Single(c => c.TeamId == team).Players);
                        return new PlayerGame(
                            g.Game.Id,
                            yourSide,
                            [.. players(ours).Where(p => p.PlayerId != playerId).Select(p => p.Name)],
                            [.. players(theirs).Select(p => p.Name)],
                            g.Game.Score,
                            g.Game.NotPlayed,
                            g.Game.Date,
                            g.Game.Court);
                    })
                    .ToList();
            items.Add(new PlayerGamesLeague(
                league.LeagueId,
                league.Name,
                league.ClubName,
                new LeagueStageSummary(stage.Number, stage.StartDate, stage.EndDate),
                division?.Number,
                StageDates.DeadlinePassed(stage.EndDate, league.TimeZone, clock),
                awaiting.UnscoredGameIds.Count,
                awaiting.Ties.Count,
                games));
        }

        return TypedResults.Ok(new GetPlayerGamesResponse(items, new PageInfo(items.Count)));
    }

    private static Guid[] SideOf(Guid team, Guid? partner) => partner is { } other ? [team, other] : [team];
}
