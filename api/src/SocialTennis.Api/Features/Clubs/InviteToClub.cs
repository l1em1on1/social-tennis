using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Membership;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// A Manager lets an email address into a Club ahead of time. Players that
/// email's User already holds become members now, including any whose request
/// was waiting; Players they add later become members as they are added
/// (<see cref="Players.CreatePlayer"/>). No email is sent: the person just
/// signs in. Inviting the same address twice is the same as once.
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

        if (!await db.ClubInvites.AnyAsync(i => i.ClubId == clubId && i.Email == email, cancellationToken))
        {
            db.ClubInvites.Add(new ClubInvite { Id = Guid.NewGuid(), ClubId = clubId, Email = email, CreatedAt = now });
        }

        var players = await db.Players
            .Where(p => p.User.Email == email)
            .Select(p => new { p.Id, IsMember = p.Memberships.Any(m => m.ClubId == clubId) })
            .ToListAsync(cancellationToken);
        foreach (var player in players.Where(p => !p.IsMember))
        {
            db.ClubMemberships.Add(ClubAdmission.Membership(clubId, player.Id, ClubMembershipStatus.Active, now));
        }

        // Waiting requests become memberships in one conditional update, so a
        // Player leaving at this moment (which takes no lock) is skipped
        // rather than failing the invite (#109).
        var waiting = players.Where(p => p.IsMember).Select(p => p.Id).ToList();
        await db.ClubMemberships
            .Where(m => m.ClubId == clubId && waiting.Contains(m.PlayerId))
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.Status, ClubMembershipStatus.Active), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
