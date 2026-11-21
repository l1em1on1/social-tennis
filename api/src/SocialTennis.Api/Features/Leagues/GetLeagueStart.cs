using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// What starting a League would do, for the Manager to change and confirm
/// (#7): the proposed ladder, by Competitor Rating (a Team's is its Players'
/// average), ties broken by earliest join time, then by Team (ADR-0013); the
/// doubles Players it would remove; the Join Requests still waiting; and the
/// first Stage's dates. Nothing changes.
/// </summary>
/// <remarks>
/// A League that has started, or can't start yet (Knockout, #12; Foursomes,
/// #14), is a 409, as confirming would be.
/// </remarks>
public static class GetLeagueStart
{
    public static async Task<Results<Ok<GetLeagueStartResponse>, NotFound, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
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

        var teams = await db.Teams
            .Where(t => t.LeagueId == leagueId)
            .Select(t => new
            {
                t.Id,
                Players = t.Members
                    .OrderBy(m => m.JoinedAt)
                    .ThenBy(m => m.PlayerId)
                    .Select(m => new { m.PlayerId, m.Player.Name, m.Player.Rating, m.JoinedAt })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);
        var competitors = teams
            .Where(t => t.Players.Count > 0)
            .Select(t => new LeagueStartCompetitor(
                t.Id,
                [.. t.Players.Select(p => new LeagueStartPlayer(p.PlayerId, p.Name))],
                t.Players.Average(p => (double)p.Rating),
                t.Players.Min(p => p.JoinedAt)))
            .OrderByDescending(c => c.Rating)
            .ThenBy(c => c.JoinedAt)
            .ThenBy(c => c.TeamId)
            .ToList();

        var (start, end) = StageDates.First(league.StartDate, league.EndDate, league.StageLength);
        return TypedResults.Ok(new GetLeagueStartResponse(
            competitors,
            await LeagueLadder.UnpairedAsync(db, leagueId, league.Type, cancellationToken),
            await db.LeagueJoinRequests.CountAsync(
                r => r.LeagueId == leagueId && r.Status == LeagueJoinRequestStatus.Pending, cancellationToken),
            new LeagueStageSummary(1, start, end)));
    }
}
