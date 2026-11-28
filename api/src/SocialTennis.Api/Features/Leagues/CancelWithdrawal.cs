using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Player takes back their Team's withdrawal request while it waits (#21):
/// it goes, as if never asked. Either partner may, in doubles. A Player the
/// User doesn't hold, or no request waiting for their Team: 404.
/// </summary>
public static class CancelWithdrawal
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid leagueId,
        Guid playerId,
        ClaimsPrincipal user,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        await using var transaction = await LeagueEntry.LockLeagueAsync(db, leagueId, cancellationToken);
        var deleted = await db.WithdrawalRequests
            .Where(r => r.LeagueId == leagueId && r.Status == WithdrawalStatus.Pending
                && db.LeagueParticipants.Any(p => p.LeagueId == leagueId && p.PlayerId == playerId && p.Player.UserId == userId && p.TeamId == r.TeamId))
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return deleted > 0 ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
