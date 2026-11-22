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
/// A League's ladder in its current Stage (#7): the Divisions from the top,
/// each with its Teams and every Game between them (ADR-0031), with its Score
/// once entered (#38). The Players in
/// the League see it, whether or not they are on the ladder, and so do Managers.
/// </summary>
/// <remarks>
/// Anyone else gets a 404, as if the League weren't there (ADR-0017): a Player
/// who isn't in the League can't see its ladder or fixtures. Before the start
/// the ladder is empty and <c>stage</c> is null.
/// </remarks>
public static class GetLeagueLadder
{
    public static async Task<Results<Ok<GetLeagueLadderResponse>, NotFound>> HandleAsync(
        Guid leagueId,
        ClaimsPrincipal user,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        var league = await db.Leagues
            .Where(l => l.Id == leagueId)
            .Select(l => new
            {
                l.Name,
                ClubName = l.Club.Name,
                l.Type,
                l.ScoreFormat,
                Yours = l.Participants.Where(p => p.Player.UserId == userId).Select(p => p.PlayerId).ToList(),
                Stage = l.Stages.OrderByDescending(s => s.Number).Select(s => new { s.Id, s.Number, s.StartDate, s.EndDate }).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (league is null || !(league.Yours.Count > 0 || user.IsInRole(Policies.Manager)))
        {
            return TypedResults.NotFound();
        }

        if (league.Stage is null)
        {
            return TypedResults.Ok(new GetLeagueLadderResponse(leagueId, league.Name, league.ClubName, league.Type, null, [], league.Yours));
        }

        var places = await db.LadderPlaces
            .Where(p => p.StageId == league.Stage.Id)
            .OrderBy(p => p.Position)
            .Select(p => new
            {
                p.Division,
                Competitor = new LadderCompetitor(
                    p.TeamId,
                    p.Position,
                    p.Team.Members
                        .OrderBy(m => m.JoinedAt)
                        .ThenBy(m => m.PlayerId)
                        .Select(m => new LadderPlayer(m.PlayerId, m.Player.Name))
                        .ToList()),
            })
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        var games = await db.Games
            .Where(g => g.StageId == league.Stage.Id)
            .Select(g => new
            {
                g.Id,
                g.Division,
                First = g.Sides.Where(s => s.Side == 1).Select(s => s.TeamId).Single(),
                Second = g.Sides.Where(s => s.Side == 2).Select(s => s.TeamId).Single(),
                Score = g.Score == null ? null : new { g.Score.Winner, g.Score.Retired, g.Score.Sets },
            })
            .ToListAsync(cancellationToken);

        // Each Division's Games in ladder order: by the higher Team's place, then the lower's.
        var positionOf = places.ToDictionary(p => p.Competitor.TeamId, p => p.Competitor.Position);
        var divisions = places
            .GroupBy(p => p.Division)
            .OrderBy(group => group.Key)
            .Select(group => new LadderDivision(
                group.Key,
                [.. group.Select(p => p.Competitor)],
                [
                    .. games
                        .Where(g => g.Division == group.Key)
                        .OrderBy(g => positionOf[g.First])
                        .ThenBy(g => positionOf[g.Second])
                        .Select(g => new LadderGame(
                            g.Id,
                            g.First,
                            g.Second,
                            g.Score is { } score
                                ? new LadderGameScore(score.Winner, score.Retired, SetScore.Of(league.ScoreFormat, score.Sets))
                                : null)),
                ]))
            .ToList();

        return TypedResults.Ok(new GetLeagueLadderResponse(
            leagueId,
            league.Name,
            league.ClubName,
            league.Type,
            new LeagueStageSummary(league.Stage.Number, league.Stage.StartDate, league.Stage.EndDate),
            divisions,
            league.Yours));
    }
}
