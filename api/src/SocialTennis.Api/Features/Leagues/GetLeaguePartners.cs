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
/// The Club members one of the signed-in User's Players could ask to join a
/// doubles League with (#6), by name: not the Player, nobody in a Team in it,
/// and nobody in a request still waiting there. The User's other Players are
/// included. Asking checks again, so the list is only a guide.
/// </summary>
/// <remarks>
/// Names only, never email addresses (owner, 2026-11-20). Another User's
/// Player, or a League at a Club the Player isn't in, is a 404 (ADR-0017); a
/// League that isn't doubles is a 409.
/// </remarks>
public static class GetLeaguePartners
{
    public static async Task<Results<Ok<GetLeaguePartnersResponse>, NotFound, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
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

        var league = await db.Leagues
            .Where(l => l.Id == leagueId)
            .Select(l => new { l.ClubId, l.Type })
            .SingleOrDefaultAsync(cancellationToken);
        if (league is null || !await LeagueEntry.IsClubMemberAsync(db, league.ClubId, playerId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        if (league.Type.TeamSize() != 2)
        {
            return LeagueTeams.NotDoubles();
        }

        var partners = await db.ClubMemberships
            .Where(m => m.ClubId == league.ClubId && m.Status == ClubMembershipStatus.Active && m.PlayerId != playerId)
            .Where(m => !db.LeagueParticipants.Any(p => p.LeagueId == leagueId && p.PlayerId == m.PlayerId && p.TeamId != null))
            .Where(m => !db.LeagueJoinRequests.Any(r =>
                r.LeagueId == leagueId
                && r.Status == LeagueJoinRequestStatus.Pending
                && (r.PlayerId == m.PlayerId || r.PartnerId == m.PlayerId)))
            .OrderBy(m => m.Player.Name)
            .ThenBy(m => m.PlayerId)
            .Select(m => new LeaguePartner(m.PlayerId, m.Player.Name))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new GetLeaguePartnersResponse(partners, new PageInfo(partners.Count)));
    }
}
