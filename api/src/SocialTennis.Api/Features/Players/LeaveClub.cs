using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;

namespace SocialTennis.Api.Features.Players;

/// <summary>
/// One of the signed-in User's Players leaves a Club, or takes back a request
/// that is still waiting: both delete the membership. Leaving a Club the Player
/// isn't in is fine.
/// </summary>
/// <remarks>
/// Coming back means asking again: an invite was used up when it let the
/// Player in (#164). An unused invite for another Club is left alone.
/// </remarks>
public static class LeaveClub
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid playerId,
        Guid clubId,
        ClaimsPrincipal user,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        if (!await db.Players.AnyAsync(p => p.Id == playerId && p.UserId == userId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        await db.ClubMemberships
            .Where(m => m.PlayerId == playerId && m.ClubId == clubId)
            .ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
