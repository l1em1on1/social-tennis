using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Membership;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// One of the signed-in User's Players asks to join a Club, through its join
/// link (#222, ADR-0032): asking needs the link's current code, so a Manager
/// turning the link off or resetting it stops new requests. The request waits
/// for a Manager's approval, unless the User is already in the Club (holds an
/// approved member of it) or a Manager has invited their email: then the
/// Player is a member at once, and the invite is used up (#164). The app is
/// informal, so a User's Players aren't guarded against each other (owner,
/// 2026-11-19). A PUT, so asking twice is the same as asking once, and never
/// undoes an approval; asking again after the User got in lets a waiting
/// request in. A link that's off, was reset, or never existed is a 404, as
/// is another User's Player.
/// </summary>
public static class JoinClub
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        string code,
        Guid playerId,
        ClaimsPrincipal user,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        var linked = await db.Clubs.Where(c => c.JoinCode == code).Select(c => (Guid?)c.Id).SingleOrDefaultAsync(cancellationToken);
        if (linked is not { } clubId)
        {
            return TypedResults.NotFound();
        }

        // Serialized with invites for this email, with a second press, and with
        // deleting this User (#116), so the Player is checked once it holds.
        var email = EmailAddress.Normalize(user.FindFirstValue(ClaimTypes.Email)!);
        await using var transaction = await ClubAdmission.LockEmailAsync(db, email, cancellationToken);
        if (!await db.Players.AnyAsync(p => p.Id == playerId && p.UserId == userId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

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

            // Let in by an invite: the User is in now, so their other waiting requests here go too.
            if (invited)
            {
                await ClubAdmission.ApproveWaitingAsync(db, userId, clubId, cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
