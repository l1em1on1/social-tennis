using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Membership;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// A Manager approves a Player's request to join. The Player's User is then in
/// the Club, so their other requests still waiting there are approved with it
/// (#164: a User in a Club brings all their Players). Approving a member again
/// changes nothing.
/// </summary>
public static class ApproveClubMember
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid clubId,
        Guid playerId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var holder = await db.Players
            .Where(p => p.Id == playerId)
            .Select(p => new { p.UserId, p.User.Email })
            .SingleOrDefaultAsync(cancellationToken);
        if (holder is null)
        {
            return TypedResults.NotFound();
        }

        // Serialized with that User asking, adding a Player and being invited.
        await using var transaction = await ClubAdmission.LockEmailAsync(db, holder.Email, cancellationToken);

        // Conditional updates: a request withdrawn (or a member removed) at the
        // same moment is a 404 or skipped, not a concurrency failure (#109).
        var approved = await db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.PlayerId == playerId)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.Status, ClubMembershipStatus.Active), cancellationToken);
        if (approved > 0)
        {
            await db.ClubMemberships
                .Where(m => m.ClubId == clubId && m.Status == ClubMembershipStatus.Pending && m.Player.UserId == holder.UserId)
                .ExecuteUpdateAsync(set => set.SetProperty(m => m.Status, ClubMembershipStatus.Active), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return approved == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }
}
