using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;

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
                .Select(s => new CurrentStage(s.Id, s.Number, s.StartDate, s.EndDate, s.ClosedAt != null))
                .FirstOrDefault())
            .SingleOrDefaultAsync(cancellationToken);

    public sealed record CurrentStage(Guid Id, int Number, DateOnly StartDate, DateOnly EndDate, bool Closed);
}
