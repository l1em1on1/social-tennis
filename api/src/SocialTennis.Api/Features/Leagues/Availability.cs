using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>What reading and saving a Player's free days (#9) share.</summary>
internal static class Availability
{
    /// <summary>
    /// The League's current Stage, if <paramref name="playerId"/> is the User's
    /// own Player and in the League; null otherwise, or before the start.
    /// </summary>
    public static Task<CurrentStage?> OpenStageAsync(
        TennisDbContext db, Guid leagueId, Guid playerId, Guid userId, CancellationToken cancellationToken) =>
        db.LeagueParticipants
            .Where(p => p.LeagueId == leagueId && p.PlayerId == playerId && p.Player.UserId == userId)
            .Select(p => p.League.Stages
                .OrderByDescending(s => s.Number)
                .Select(s => new CurrentStage(s.Id, s.Number, s.StartDate, s.EndDate, s.ClosedAt != null, p.League.EndDate, p.League.Club.TimeZone))
                .FirstOrDefault())
            .SingleOrDefaultAsync(cancellationToken);

    /// <param name="LeagueEndDate">The League's last day.</param>
    /// <param name="TimeZone">The Club's, for today (ADR-0016).</param>
    public sealed record CurrentStage(
        Guid Id, int Number, DateOnly StartDate, DateOnly EndDate, bool Closed, DateOnly LeagueEndDate, string TimeZone)
    {
        /// <summary>
        /// The last day that can be ticked: the Stage's end, or once its deadline has passed and it is still
        /// open (held up, #10), the League's end, so its Games can still be arranged.
        /// </summary>
        public DateOnly LastDay(TimeProvider clock) =>
            ClubCalendar.Today(TimeZone, clock) > EndDate ? LeagueEndDate : EndDate;
    }
}
