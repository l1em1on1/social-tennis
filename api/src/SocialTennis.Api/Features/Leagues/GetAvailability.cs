using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Player's days in their League's current Stage (#9, #276): each date
/// with what counts on it, their own setting for it or else their usual week
/// (<see cref="Players.GetUsualWeek"/>). Only their own: nobody else reads a
/// Player's whole list. A Game's other Players see only the times everyone in
/// it shares (<see cref="Games.GetGame"/>), which shows them where theirs overlap.
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

        var own = await db.AvailableDays.AsNoTracking().Where(d => d.StageId == stage.Id && d.PlayerId == playerId).ToListAsync(cancellationToken);
        var usual = await db.UsualDays.AsNoTracking().Where(d => d.PlayerId == playerId).ToListAsync(cancellationToken);
        var time = PlayerFreeTime.Of(own, usual);
        var lastDay = stage.LastDay(clock);
        var days = new List<AvailabilityDay>();
        for (var date = stage.StartDate; date <= lastDay; date = date.AddDays(1))
        {
            var window = time.On(date);
            days.Add(new AvailabilityDay(date, time.Own.ContainsKey(date), window is not null, window?.From, window?.To));
        }

        return TypedResults.Ok(new GetAvailabilityResponse(
            new LeagueStageSummary(stage.Number, stage.StartDate, stage.EndDate), days, lastDay));
    }
}
