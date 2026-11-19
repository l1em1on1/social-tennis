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
/// for a Manager's approval, unless the User is already in the Club (holds an
/// approved member of it) or a Manager has invited their email: then the
/// Player is a member at once, and the invite is used up (#164). The app is
/// informal, so a User's Players aren't guarded against each other (owner,
/// 2026-11-19). A PUT, so asking twice is the same as asking once, and never
/// undoes an approval; asking again after the User got in lets a waiting
/// request in.
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

        var membership = await db.ClubMemberships
            .SingleOrDefaultAsync(m => m.ClubId == clubId && m.PlayerId == playerId, cancellationToken);
        if (membership is null || membership.Status == ClubMembershipStatus.Pending)
        {
            var invited = await ClubAdmission.UseUpInvitesAsync(db, email, cancellationToken, clubId) > 0;
            var userIsIn = await db.ClubMemberships.AnyAsync(
                m => m.ClubId == clubId && m.Player.UserId == userId && m.Status == ClubMembershipStatus.Active,
                cancellationToken);
            if (membership is null)
            {
                db.ClubMemberships.Add(ClubAdmission.Membership(
                    clubId,
                    playerId,
                    invited || userIsIn ? ClubMembershipStatus.Active : ClubMembershipStatus.Pending,
                    clock.GetUtcNow()));
            }
            else if (invited || userIsIn)
            {
                // Asked before the User was in: asking again lets it in now.
                membership.Status = ClubMembershipStatus.Active;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
