using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Manager breaks up a Team in a doubles League (#6): both Players stay in
/// the League, unpaired, to be paired again. A DELETE: a Team that isn't there
/// changes nothing.
/// </summary>
/// <remarks>
/// A Team on the started League's ladder, or in a Game, is a 409 (#7): a
/// partner change is then a Withdrawal and a fresh join (#21). A Team the
/// Manager left out, or that came in after the start, can still be broken up.
/// </remarks>
public static class UnpairTeam
{
    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
        Guid teamId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        await using var transaction = await LeagueEntry.LockLeagueAsync(db, leagueId, cancellationToken);
        var type = await db.Leagues
            .Where(l => l.Id == leagueId)
            .Select(l => (LeagueType?)l.Type)
            .SingleOrDefaultAsync(cancellationToken);
        if (type is null)
        {
            return TypedResults.NotFound();
        }
        if (type.Value.TeamSize() != 2)
        {
            return LeagueTeams.NotDoubles();
        }
        if (await LeagueLadder.IsInPlayAsync(db, leagueId, teamId, cancellationToken))
        {
            return LeagueLadder.InPlay();
        }
        await LeagueTeams.BreakUpAsync(db, leagueId, teamId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
