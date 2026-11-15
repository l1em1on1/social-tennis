using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.Features.Players;

/// <summary>
/// The holding User changes a Player's Name, Gender and Level. Another User's
/// Player is "not found", the same answer as one that doesn't exist, so ids
/// can't be probed. Rating is not theirs to change (<see cref="SetPlayerRating"/>).
/// </summary>
public static class UpdatePlayer
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid playerId,
        UpdatePlayerRequest request,
        ClaimsPrincipal user,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        var player = await db.Players
            .SingleOrDefaultAsync(p => p.Id == playerId && p.UserId == userId, cancellationToken);
        if (player is null)
        {
            return TypedResults.NotFound();
        }

        player.Name = request.Name.Trim();
        player.Gender = request.Gender;
        player.Level = request.Level;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
