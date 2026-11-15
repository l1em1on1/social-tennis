using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>A Manager approves a Player's request to join. Approving a member again changes nothing.</summary>
public static class ApproveClubMember
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid clubId,
        Guid playerId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        // One conditional update: a request withdrawn (or a member removed) at
        // the same moment is a 404, not a concurrency failure (#109).
        var approved = await db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.PlayerId == playerId)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.Status, ClubMembershipStatus.Active), cancellationToken);
        return approved == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }
}
