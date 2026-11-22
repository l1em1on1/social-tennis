using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Games.Contracts;

namespace SocialTennis.Api.Features.Games;

/// <summary>
/// A Manager overrides a Game's Score (#38; ADR-0008, Phasing), whether its
/// Stage is open or closed. The override is kept in the Score's history, as
/// the Manager's, with the old value.
/// </summary>
/// <remarks>
/// Managers only, by the routing table's policy (ADR-0017). A Manager who
/// plays in the Game corrects it as their Player while the Stage is open,
/// like anyone in it, and overrides it after.
/// <list type="bullet">
/// <item>No such Game: 404.</item>
/// <item>No Score yet: 409 <c>not-scored</c>. Recording a result for an unplayed Game is #10's resolution queue.</item>
/// <item>An override made from an older version than the Score's: 409 <c>score-changed</c> (ADR-0018).</item>
/// <item>The format and date rules of entering one: 400 under the wire name.</item>
/// </list>
/// </remarks>
public static class OverrideScore
{
    public static async Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> HandleAsync(
        Guid gameId,
        OverrideScoreRequest request,
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
            .Select(g => new
            {
                g.LeagueId,
                g.Stage.Number,
                g.Stage.StartDate,
                g.Stage.League.Club.TimeZone,
                g.Stage.League.ScoreFormat,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (game is null)
        {
            return TypedResults.NotFound();
        }

        var score = await db.Scores.SingleOrDefaultAsync(s => s.LeagueId == game.LeagueId && s.GameId == gameId, cancellationToken);
        if (score is null)
        {
            return GameScores.NotScored();
        }

        var today = ClubCalendar.Today(game.TimeZone, clock);
        var (winner, errors) = GameScores.Check(game.ScoreFormat, game.Number, game.StartDate, today, request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (await GameScores.ChangeAsync(
                db, score, request, request.Version, winner, ScoreChangeKind.Overridden, clock.GetUtcNow(), user.UserId(), null, cancellationToken) is { } refused)
        {
            return refused;
        }

        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
