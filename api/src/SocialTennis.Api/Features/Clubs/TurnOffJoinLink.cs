using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// A Manager turns the Club's join link off (#222): its link stops working,
/// and nobody can ask to join until a Manager turns it on again, with a new
/// code. Turning off a link that's already off is fine.
/// </summary>
public static class TurnOffJoinLink
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid clubId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var updated = await db.Clubs
            .Where(c => c.Id == clubId)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.JoinCode, (string?)null), cancellationToken);
        return updated == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }
}
