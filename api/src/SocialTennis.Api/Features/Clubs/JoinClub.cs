using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Membership;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// One of the signed-in User's Players asks to join a Club. The request waits
/// for a Manager's approval, unless a Manager has invited the User's email, in
/// which case the Player is a member at once and the invite is used up (#164).
/// A sibling Player being a member doesn't let this one in: only Players the
/// User adds later join on their own, so a Manager's removal sticks. A PUT, so
/// asking twice is the same as asking once, and never undoes an approval.
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

        // Serialized with invites for this email, and with a second press.
        var email = EmailAddress.Normalize(user.FindFirstValue(ClaimTypes.Email)!);
        await using var transaction = await ClubAdmission.LockEmailAsync(db, email, cancellationToken);

        if (!await db.ClubMemberships.AnyAsync(m => m.ClubId == clubId && m.PlayerId == playerId, cancellationToken))
        {
            var invited = await ClubAdmission.UseUpInvitesAsync(db, email, cancellationToken, clubId) > 0;
            db.ClubMemberships.Add(ClubAdmission.Membership(
                clubId,
                playerId,
                invited ? ClubMembershipStatus.Active : ClubMembershipStatus.Pending,
                clock.GetUtcNow()));
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
