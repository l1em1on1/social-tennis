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
/// The Leagues one of the signed-in User's Players is in, and the Leagues at
/// Clubs it is a member of that it could join, with its own Join Request on
/// each. Latest start first. Another User's Player is a 404.
/// </summary>
public static class GetPlayerLeagues
{
    public static async Task<Results<Ok<GetPlayerLeaguesResponse>, NotFound>> HandleAsync(
        Guid playerId,
        ClaimsPrincipal user,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        if (!await db.Players.AnyAsync(p => p.Id == playerId && p.UserId == userId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var leagues = await db.Leagues
            .Where(l => l.Participants.Any(p => p.PlayerId == playerId)
                || db.ClubMemberships.Any(m =>
                    m.ClubId == l.ClubId && m.PlayerId == playerId && m.Status == ClubMembershipStatus.Active))
            .OrderByDescending(l => l.StartDate)
            .ThenBy(l => l.Name)
            .Select(l => new PlayerLeague(
                l.Id,
                l.ClubId,
                l.Club.Name,
                l.Name,
                l.Type,
                l.Structure,
                l.StartDate,
                l.EndDate,
                l.StageLength,
                l.StageRoll,
                l.ScoreFormat,
                l.Participants.Count,
                l.Participants.Any(p => p.PlayerId == playerId),
                l.JoinRequests
                    .Where(r => r.PlayerId == playerId)
                    .Select(r => new PlayerLeagueJoinRequest(r.Status, r.RequestedAt, r.DecidedAt))
                    .SingleOrDefault()))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new GetPlayerLeaguesResponse(leagues, new PageInfo(leagues.Count)));
    }
}
