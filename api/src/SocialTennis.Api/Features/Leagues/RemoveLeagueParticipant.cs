using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Manager takes a Player out of a League before it starts (#159): someone
/// added by mistake, or who dropped out. A DELETE: removing someone who isn't
/// in it changes nothing.
/// </summary>
/// <remarks>
/// Leagues can't start yet (#7). Once they can, this refuses a started League:
/// leaving mid-season is a Withdrawal (#21).
/// <para>
/// The Player's approved Join Request goes too, so they are back where they
/// were before asking and may ask again. Kept, it would say "approved" while
/// they aren't in, and block a new request. A rejected request stays: a Player
/// a Manager turned down, then added, then removed, still can't ask.
/// </para>
/// </remarks>
public static class RemoveLeagueParticipant
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid leagueId,
        Guid playerId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        await using var transaction = await LeagueEntry.LockLeagueAsync(db, leagueId, cancellationToken);
        if (!await db.Leagues.AnyAsync(l => l.Id == leagueId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var removed = await db.LeagueParticipants
            .Where(p => p.LeagueId == leagueId && p.PlayerId == playerId)
            .ExecuteDeleteAsync(cancellationToken);
        if (removed > 0)
        {
            await db.LeagueJoinRequests
                .Where(r => r.LeagueId == leagueId && r.PlayerId == playerId && r.Status == LeagueJoinRequestStatus.Approved)
                .ExecuteDeleteAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
