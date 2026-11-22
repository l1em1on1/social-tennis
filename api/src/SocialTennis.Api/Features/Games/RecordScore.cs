using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Games.Contracts;

namespace SocialTennis.Api.Features.Games;

/// <summary>
/// A Manager records the Score of a Game nobody has scored (#10): one of the
/// ways to settle what holds up a Stage's close. It names no Player, and its
/// history starts with the Manager's entry.
/// </summary>
/// <remarks>
/// Managers only, by the routing table's policy (ADR-0017).
/// <list type="bullet">
/// <item>No such Game: 404.</item>
/// <item>A Game with a Score: 409 <c>already-scored</c>, also when a Player enters one at the same time. Change one with an override.</item>
/// <item>A Game marked not played: 409 <c>not-played</c>; undo that first.</item>
/// <item>A Game whose Stage has closed: 409 <c>stage-closed</c>.</item>
/// <item>The format and date rules of entering one: 400 under the wire name.</item>
/// </list>
/// </remarks>
public static class RecordScore
{
    public static async Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> HandleAsync(
        Guid gameId,
        RecordScoreRequest request,
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
                Closed = g.Stage.ClosedAt != null,
                g.Stage.League.Club.TimeZone,
                g.Stage.League.ScoreFormat,
                Scored = g.Score != null,
                Voided = g.VoidedAt != null,
            })
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

        if (game.Voided)
        {
            return GameScores.NotPlayed();
        }

        var today = ClubCalendar.Today(game.TimeZone, clock);
        var (winner, errors) = GameScores.Check(game.ScoreFormat, game.Number, game.StartDate, today, request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var userId = user.UserId();
        var now = clock.GetUtcNow();
        var score = new Score
        {
            GameId = gameId,
            LeagueId = game.LeagueId,
            PlayedOn = request.PlayedOn,
            Sets = [.. request.Sets],
            Winner = winner,
            Retired = request.RetirementWinner is not null,
            SubmittedByUserId = userId,
            SubmittedAt = now,
        };
        db.Scores.Add(score);
        db.ScoreChanges.Add(new ScoreChange
        {
            Id = Guid.NewGuid(),
            LeagueId = game.LeagueId,
            GameId = gameId,
            Kind = ScoreChangeKind.Recorded,
            At = now,
            ByUserId = userId,
            After = ScoreValue.Of(score),
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A Player entered it between the check above and this save.
            return GameScores.AlreadyScored();
        }

        return TypedResults.NoContent();
    }
}
