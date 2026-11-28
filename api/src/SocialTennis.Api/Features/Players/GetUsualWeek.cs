using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.Features.Players;

/// <summary>
/// A Player's usual week (#276), for the User who holds them: a parent reads
/// each child's. Nobody else reads it; a Game's other Players see only the
/// times everyone in it shares (<see cref="Games.GetGame"/>).
/// </summary>
/// <remarks>Another User's Player, or none: 404.</remarks>
public static class GetUsualWeek
{
    public static async Task<Results<Ok<UsualWeek>, NotFound>> HandleAsync(
        Guid playerId,
        ClaimsPrincipal user,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        if (!await db.Players.AnyAsync(p => p.Id == playerId && p.UserId == userId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var days = await db.UsualDays
            .Where(d => d.PlayerId == playerId)
            .OrderBy(d => d.Weekday)
            .Select(d => new UsualWeekDay(d.Weekday, d.From, d.To))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(new UsualWeek(days));
    }
}
