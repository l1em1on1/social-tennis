using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// A Manager declines a request to join, or removes a member: both delete the
/// row. The Player can ask again. Removing someone already gone is fine.
/// </summary>
public static class RemoveClubMember
{
    public static async Task<NoContent> HandleAsync(
        Guid clubId,
        Guid playerId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        await db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.PlayerId == playerId)
            .ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
