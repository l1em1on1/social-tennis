using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.Features.Players;

/// <summary>
/// The holding User sets a Player's usual week (#276): each weekday not free,
/// free any time, or free in a window. It stands for every date the Player
/// hasn't set in each of their Leagues' Stages, so most members set it once.
/// The whole week each time: it replaces the one before.
/// </summary>
/// <remarks>
/// Another User's Player, or none: 404. Two saves at once take turns (<see cref="UsualWeekLock"/>).
/// </remarks>
public static class SetUsualWeek
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid playerId,
        SetUsualWeekRequest request,
        ClaimsPrincipal user,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        await using var transaction = await UsualWeekLock.BeginAsync(db, playerId, cancellationToken);
        if (!await db.Players.AnyAsync(p => p.Id == playerId && p.UserId == userId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        await db.UsualDays.Where(d => d.PlayerId == playerId).ExecuteDeleteAsync(cancellationToken);
        db.UsualDays.AddRange(request.Days.Select(day => new UsualDay { PlayerId = playerId, Weekday = day.Weekday, From = day.From, To = day.To }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
