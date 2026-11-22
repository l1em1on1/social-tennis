using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;

namespace SocialTennis.Api.Features.Games;

/// <summary>
/// A Manager undoes marking a Game not played (#10), while its Stage is
/// open: it can be scored again, and holds up the close until it is or is
/// marked again. Undoing one that isn't marked changes nothing.
/// </summary>
/// <remarks>Managers only, by the routing table's policy. No such Game: 404. A closed Stage: 409 <c>stage-closed</c>.</remarks>
public static class UnvoidGame
{
    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
        Guid gameId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        if (await GameScores.LeagueOfAsync(db, gameId, cancellationToken) is not { } leagueId)
        {
            return TypedResults.NotFound();
        }

        await using var transaction = await GameScores.LockResultsAsync(db, leagueId, cancellationToken);
        var game = await db.Games
            .Where(g => g.Id == gameId)
            .Select(g => new { Closed = g.Stage.ClosedAt != null })
            .SingleOrDefaultAsync(cancellationToken);
        if (game is null)
        {
            return TypedResults.NotFound();
        }

        if (game.Closed)
        {
            return GameScores.StageClosed();
        }

        await db.Games
            .Where(g => g.Id == gameId)
            .ExecuteUpdateAsync(set => set.SetProperty(g => g.VoidedAt, (DateTimeOffset?)null).SetProperty(g => g.VoidedByUserId, (Guid?)null), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
