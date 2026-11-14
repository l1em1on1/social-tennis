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
        var membership = await db.ClubMemberships
            .SingleOrDefaultAsync(m => m.ClubId == clubId && m.PlayerId == playerId, cancellationToken);
        if (membership is null)
        {
            return TypedResults.NotFound();
        }

        membership.Status = ClubMembershipStatus.Active;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
