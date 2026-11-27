using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Player's free days in their League's current Stage (#9), for them to
/// tick and untick. Only their own: nobody else reads a Player's whole list.
/// A Game's other Players see only the days everyone in it shares
/// (<see cref="Games.GetGame"/>), which shows them where theirs overlap.
/// </summary>
/// <remarks>
/// A Player the signed-in User doesn't hold, one not in the League, or a League
/// with no open Stage (not started, or ended): 404.
/// </remarks>
public static class GetAvailability
{
    public static async Task<Results<Ok<GetAvailabilityResponse>, NotFound>> HandleAsync(
        Guid leagueId,
        Guid playerId,
        ClaimsPrincipal user,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (await Availability.OpenStageAsync(db, leagueId, playerId, user.UserId(), cancellationToken) is not { Closed: false } stage)
        {
            return TypedResults.NotFound();
        }

        var dates = await db.AvailableDays
            .Where(d => d.StageId == stage.Id && d.PlayerId == playerId)
            .OrderBy(d => d.Date)
            .Select(d => d.Date)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(new GetAvailabilityResponse(
            new LeagueStageSummary(stage.Number, stage.StartDate, stage.EndDate), dates, stage.LastDay(clock)));
    }
}
