using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// One of the signed-in User's Players asks to join a Club. The request waits
/// for a Manager's approval, unless a Manager has already invited the User's
/// email, in which case the Player is a member at once. A PUT, so asking twice
/// is the same as asking once, and never undoes an approval.
/// </summary>
public static class JoinClub
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid clubId,
        Guid playerId,
        ClaimsPrincipal user,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        var held = await db.Players.AnyAsync(p => p.Id == playerId && p.UserId == userId, cancellationToken);
        if (!held || !await db.Clubs.AnyAsync(c => c.Id == clubId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        if (!await db.ClubMemberships.AnyAsync(m => m.ClubId == clubId && m.PlayerId == playerId, cancellationToken))
        {
            var email = ClubAdmission.NormalizeEmail(user.FindFirstValue(ClaimTypes.Email)!);
            var invited = await db.ClubInvites.AnyAsync(i => i.ClubId == clubId && i.Email == email, cancellationToken);
            db.ClubMemberships.Add(ClubAdmission.Membership(
                clubId,
                playerId,
                invited ? ClubMembershipStatus.Active : ClubMembershipStatus.Pending,
                clock.GetUtcNow()));
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Two presses raced; the other one's row is the request.
            }
        }

        return TypedResults.NoContent();
    }
}
