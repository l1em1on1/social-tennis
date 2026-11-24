using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Membership;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// A Manager lets an email address into a Club ahead of time. If that email's
/// User already holds Players, they become members now, including any whose
/// request was waiting, and nothing is kept: the invite is used up at once.
/// Otherwise the invite waits for the first Player, who joins as it is added
/// (<see cref="ClubAdmission.AdmitNewPlayerAsync"/>), and later Players follow
/// it in (#164). No email is sent: the person just signs in. Inviting the same
/// address twice is the same as once.
/// </summary>
public static class InviteToClub
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid clubId,
        InviteToClubRequest request,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!await db.Clubs.AnyAsync(c => c.Id == clubId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var email = EmailAddress.Normalize(request.Email);
        var now = clock.GetUtcNow();

        // Serialized with that User asking to join or adding a Player, and with
        // another Manager inviting the same address.
        await using var transaction = await ClubAdmission.LockEmailAsync(db, email, cancellationToken);

        var held = await ClubAdmission.LetInAsync(db, email, clubId, now, cancellationToken);

        // An invite exists only while its User holds no Player.
        if (held > 0)
        {
            await ClubAdmission.UseUpInvitesAsync(db, email, cancellationToken, clubId);
        }
        else if (!await db.ClubInvites.AnyAsync(i => i.ClubId == clubId && i.Email == email, cancellationToken))
        {
            db.ClubInvites.Add(new ClubInvite { Id = Guid.NewGuid(), ClubId = clubId, Email = email, CreatedAt = now });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
