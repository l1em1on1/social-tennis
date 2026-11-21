using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A League's ladder in its current Stage (#7): the Divisions from the top,
/// each with its Competitors and Game, and the Bye. The Players in the League
/// see it, whether or not they are on the ladder, and so do Managers.
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
                InIt = l.Participants.Any(p => p.Player.UserId == userId),
                Stage = l.Stages.OrderByDescending(s => s.Number).Select(s => new { s.Id, s.Number, s.StartDate, s.EndDate }).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (league is null || !(league.InIt || user.IsInRole(Policies.Manager)))
        {
            return TypedResults.NotFound();
        }

        if (league.Stage is null)
        {
            return TypedResults.Ok(new GetLeagueLadderResponse(leagueId, league.Name, league.ClubName, league.Type, null, [], null));
        }

        var places = await db.LadderPlaces
            .Where(p => p.StageId == league.Stage.Id)
            .OrderBy(p => p.Position)
            .Select(p => new LadderCompetitor(
                p.TeamId,
                p.Position,
                p.Team.Members
                    .OrderBy(m => m.JoinedAt)
                    .ThenBy(m => m.PlayerId)
                    .Select(m => new LadderPlayer(m.PlayerId, m.Player.Name))
                    .ToList()))
            .ToListAsync(cancellationToken);
        var games = await db.Games
            .Where(g => g.StageId == league.Stage.Id)
            .ToDictionaryAsync(g => g.Division, g => g.Id, cancellationToken);

        var size = league.Type.DivisionSize();
        var divisions = places
            .Select(place => (Place: place, Division: Ladder.DivisionOf(place.Position, places.Count, size)))
            .Where(entry => entry.Division is not null)
            .GroupBy(entry => entry.Division!.Value)
            .Select(group => new LadderDivision(group.Key, [.. group.Select(entry => entry.Place)], games[group.Key]))
            .ToList();
        var bye = places.FirstOrDefault(place => Ladder.DivisionOf(place.Position, places.Count, size) is null);

        return TypedResults.Ok(new GetLeagueLadderResponse(
            leagueId,
            league.Name,
            league.ClubName,
            league.Type,
            new LeagueStageSummary(league.Stage.Number, league.Stage.StartDate, league.Stage.EndDate),
            divisions,
            bye));
    }
}
