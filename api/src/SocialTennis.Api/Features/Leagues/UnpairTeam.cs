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
/// Leagues can't start yet (#7); once they can, this is refused after the
/// start: a partner change is then a Withdrawal and a fresh join (#21).
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
        await LeagueTeams.BreakUpAsync(db, leagueId, teamId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
