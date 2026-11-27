using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>What the withdrawal endpoints (#21) share.</summary>
internal static class Withdrawals
{
    /// <summary>
    /// The League's current Stage, if it is open and <paramref name="teamId"/> has a place on it;
    /// null otherwise (not started, ended, or the Team is off the ladder).
    /// </summary>
    public static Task<Guid?> OpenStageWithAsync(TennisDbContext db, Guid leagueId, Guid teamId, CancellationToken cancellationToken) =>
        db.Stages
            .Where(s => s.LeagueId == leagueId && s.ClosedAt == null
                && !db.Stages.Any(n => n.LeagueId == leagueId && n.Number > s.Number)
                && s.Places.Any(p => p.TeamId == teamId))
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public static ProblemHttpResult NotOnTheLadder() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "not-on-the-ladder",
            title: "Only a Team on the ladder of a running League withdraws.",
            detail: "Before the start, or off the ladder, the Manager removes a Player instead.");

    public static ProblemHttpResult AlreadyWithdrawn() =>
        TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, type: "already-withdrawn", title: "This Team has already withdrawn.");
}
