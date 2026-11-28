using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Games;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Player sets the dates of their League's current Stage that differ from
/// their usual week (#9, #276): each not free, free any time, or free in a
/// window, every one of them, replacing what they set before. A date left out
/// follows the usual week again. Days already past may stay set: it's the
/// same list sent back. A Stage held up past its deadline (#10) takes days up
/// to the League's end, so its Games can still be arranged.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A Player the signed-in User doesn't hold, one not in the League, or a League not started: 404.</item>
/// <item>The League has ended: 409 <c>stage-closed</c>.</item>
/// <item>A day outside the Stage: 400 under <c>days</c>.</item>
/// </list>
/// It holds the League's results lock, so two saves at once don't both write.
/// </remarks>
public static class SetAvailability
{
    public static async Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
        Guid playerId,
        SetAvailabilityRequest request,
        ClaimsPrincipal user,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        await using var transaction = await GameScores.LockResultsAsync(db, leagueId, cancellationToken);
        if (await Availability.OpenStageAsync(db, leagueId, playerId, user.UserId(), cancellationToken) is not { } stage)
        {
            return TypedResults.NotFound();
        }

        if (stage.Closed)
        {
            return StageClose.AlreadyClosed();
        }

        var lastDay = stage.LastDay(clock);
        if (request.Days.Any(day => day.Date < stage.StartDate || day.Date > lastDay))
        {
            var format = (DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["days"] = [$"Pick days in Stage {stage.Number}: from {format(stage.StartDate)} to {format(lastDay)}."],
            });
        }

        await db.AvailableDays.Where(d => d.StageId == stage.Id && d.PlayerId == playerId).ExecuteDeleteAsync(cancellationToken);
        db.AvailableDays.AddRange(request.Days.Select(day => new AvailableDay
        {
            StageId = stage.Id,
            PlayerId = playerId,
            Date = day.Date,
            NotFree = !day.Free,
            From = day.From,
            To = day.To,
        }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
