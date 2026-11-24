using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Membership;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// A Manager declines a request to join, or removes a member: both delete the
/// row. The Player can ask again, and a Manager decides again. Removing someone
/// already gone is fine.
/// </summary>
/// <remarks>
/// Any invite left for the holder's email is deleted too, so it can't let the
/// Player straight back in. Invites are used up on joining (#164), so normally
/// there is none. The User's other Players stay, and while one of them is a
/// member, this Player can come back by asking (the app is informal).
/// </remarks>
public static class RemoveClubMember
{
    public static async Task<NoContent> HandleAsync(
        Guid clubId,
        Guid playerId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var player = await db.Players
            .Where(p => p.Id == playerId)
            .Select(p => new { Email = p.User != null ? p.User.Email : null })
            .SingleOrDefaultAsync(cancellationToken);
        if (player is null)
        {
            return TypedResults.NoContent();
        }

        // A Former player (#116) has no email, so nothing to serialize with: just remove it.
        if (player.Email is not { } email)
        {
            await db.ClubMemberships
                .Where(m => m.ClubId == clubId && m.PlayerId == playerId)
                .ExecuteDeleteAsync(cancellationToken);
            return TypedResults.NoContent();
        }

        await using var transaction = await ClubAdmission.LockEmailAsync(db, email, cancellationToken);
        await db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.PlayerId == playerId)
            .ExecuteDeleteAsync(cancellationToken);
        await db.ClubInvites
            .Where(i => i.ClubId == clubId && i.Email == email)
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
