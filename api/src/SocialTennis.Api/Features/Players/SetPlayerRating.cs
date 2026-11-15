using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.Features.Players;

/// <summary>
/// A Manager sets a Player's Rating by hand. This is the only writer of Rating
/// there is: nothing derives it from Level, results or Scores (ADR-0007).
/// </summary>
public static class SetPlayerRating
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid playerId,
        SetPlayerRatingRequest request,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var updated = await db.Players
            .Where(p => p.Id == playerId)
            .ExecuteUpdateAsync(player => player.SetProperty(p => p.Rating, request.Rating), cancellationToken);
        return updated == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }
}
