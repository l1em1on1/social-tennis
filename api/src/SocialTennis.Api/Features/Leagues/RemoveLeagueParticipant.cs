using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Manager takes a Player out of a League before they play (#159): someone
/// added by mistake, or who dropped out. A DELETE: removing someone who isn't
/// in it changes nothing.
/// </summary>
/// <remarks>
/// Once the League has started (#7), a Player whose Team is on the ladder, or
/// in a Game, can't be removed: leaving mid-season is a Withdrawal (#21). One
/// the Manager left out, or who came in after the start, can.
/// <para>
/// The Player's approved Join Request goes too, so they are back where they
/// were before asking and may ask again. Kept, it would say "approved" while
/// they aren't in, and block a new request. A rejected request stays: a Player
/// a Manager turned down, then added, then removed, still can't ask.
/// </para>
/// <para>
/// Their Team goes with them: a Team of one is deleted, and a doubles partner
/// goes back to the unpaired Players (#6).
/// </para>
/// </remarks>
public static class RemoveLeagueParticipant
{
    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
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

        var teamId = await db.LeagueParticipants
            .Where(p => p.LeagueId == leagueId && p.PlayerId == playerId)
            .Select(p => p.TeamId)
            .SingleOrDefaultAsync(cancellationToken);
        if (teamId is { } placed && await LeagueLadder.IsInPlayAsync(db, leagueId, placed, cancellationToken))
        {
            return LeagueLadder.InPlay();
        }

        await LeagueEntry.RemoveAsync(db, leagueId, playerId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
