using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;

namespace SocialTennis.Api.Features.Games;

/// <summary>
/// A Manager marks a Game not played (see CONTEXT.md, Void; #10): it counts
/// for nothing, and takes no Score, so it no longer holds up the Stage's
/// close. Marking it again changes nothing.
/// </summary>
/// <remarks>
/// Managers only, by the routing table's policy (ADR-0017). No such Game: 404.
/// A Game with a Score: 409 <c>already-scored</c>. A closed Stage: 409 <c>stage-closed</c>.
/// </remarks>
public static class VoidGame
{
    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
        Guid gameId,
        ClaimsPrincipal user,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (await GameScores.LeagueOfAsync(db, gameId, cancellationToken) is not { } leagueId)
        {
            return TypedResults.NotFound();
        }

        await using var transaction = await GameScores.LockResultsAsync(db, leagueId, cancellationToken);
        var game = await db.Games
            .Where(g => g.Id == gameId)
            .Select(g => new { Scored = g.Score != null, Closed = g.Stage.ClosedAt != null })
            .SingleOrDefaultAsync(cancellationToken);
        if (game is null)
        {
            return TypedResults.NotFound();
        }

        if (game.Scored)
        {
            return GameScores.AlreadyScored();
        }

        if (game.Closed)
        {
            return GameScores.StageClosed();
        }

        var userId = user.UserId();
        var now = clock.GetUtcNow();
        // Conditional, so an earlier mark stays as it was.
        await db.Games
            .Where(g => g.Id == gameId && g.VoidedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(g => g.VoidedAt, now).SetProperty(g => g.VoidedByUserId, userId), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
