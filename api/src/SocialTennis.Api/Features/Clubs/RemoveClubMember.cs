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
/// An invite for the holder's email is withdrawn too. Otherwise it would let
/// the Player straight back in on their next request, and admit every Player
/// that User adds later, which is not what removing someone means.
/// </remarks>
public static class RemoveClubMember
{
    public static async Task<NoContent> HandleAsync(
        Guid clubId,
        Guid playerId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var email = await db.Players
            .Where(p => p.Id == playerId)
            .Select(p => p.User.Email)
            .SingleOrDefaultAsync(cancellationToken);
        if (email is null)
        {
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
