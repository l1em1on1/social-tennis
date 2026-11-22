using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Games.Contracts;

namespace SocialTennis.Api.Features.Games;

/// <summary>
/// What entering, correcting and overriding a Score share (#38): the checks
/// on what was entered, the change with its history entry, and the refusals,
/// each with its stable <c>type</c> (ADR-0017).
/// </summary>
public static class GameScores
{
    public static ProblemHttpResult NotInGame() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            type: "not-in-game",
            title: "Only a Player in this game can enter its score.");

    public static ProblemHttpResult AlreadyScored() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "already-scored",
            title: "This game already has a score.");

    public static ProblemHttpResult NotScored() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "not-scored",
            title: "This game has no score to change yet.");

    public static ProblemHttpResult StageClosed() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "stage-closed",
            title: "This game's Stage has closed, so only a Manager can change its score.");

    public static ProblemHttpResult ScoreChanged() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "score-changed",
            title: "The score changed after you opened it.",
            detail: "Open it again to see it as it is now, and make your change again.");

    /// <summary>
    /// The entry's Sets against the League's format (<see cref="ScoreRules"/>), and its Date
    /// played from the Stage's first day to today where the Club is. The winning Side, and the
    /// problems by wire name: none when it can be saved.
    /// </summary>
    public static (int Winner, Dictionary<string, string[]> Errors) Check(
        ScoreFormat format, int stageNumber, DateOnly stageStart, DateOnly today, IScoreEntry entry)
    {
        var errors = new Dictionary<string, string[]>();
        var check = ScoreRules.Check(format, entry.Sets, entry.RetirementWinner);
        if (!check.IsValid)
        {
            errors[check.Field!] = [check.Message!];
        }

        if (entry.PlayedOn < stageStart)
        {
            // Month names in English whatever the server's culture: the app's language.
            errors["playedOn"] =
            [
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Stage {stageNumber} started on {stageStart:d MMM yyyy}: the game can't have been played before that."),
            ];
        }
        else if (entry.PlayedOn > today)
        {
            errors["playedOn"] = ["The date played can't be in the future."];
        }

        return (check.Winner, errors);
    }

    /// <summary>
    /// Changes <paramref name="score"/> to the entry, made from <paramref name="version"/>, and
    /// records it as <paramref name="kind"/> by <paramref name="byUserId"/> (acting as
    /// <paramref name="byPlayerId"/>, unless a Manager's override), with the value before. An entry
    /// that says what the Score already does changes and records nothing. Null once saved; the
    /// refusal when the Score has changed since that version.
    /// </summary>
    public static async Task<ProblemHttpResult?> ChangeAsync(
        TennisDbContext db,
        Score score,
        IScoreEntry entry,
        uint version,
        int winner,
        ScoreChangeKind kind,
        DateTimeOffset at,
        Guid byUserId,
        Guid? byPlayerId,
        CancellationToken cancellationToken)
    {
        var before = ScoreValue.Of(score);
        var after = new ScoreValue
        {
            PlayedOn = entry.PlayedOn,
            Sets = [.. entry.Sets],
            Winner = winner,
            Retired = entry.RetirementWinner is not null,
        };
        if (before.PlayedOn == after.PlayedOn && before.Winner == after.Winner && before.Retired == after.Retired
            && before.Sets.SequenceEqual(after.Sets))
        {
            return null;
        }

        // The UPDATE is conditioned on the version the client saw, not the one just loaded (ADR-0018).
        db.Entry(score).Property(s => s.Version).OriginalValue = version;
        score.PlayedOn = after.PlayedOn;
        score.Sets = [.. after.Sets];
        score.Winner = after.Winner;
        score.Retired = after.Retired;
        db.ScoreChanges.Add(new ScoreChange
        {
            Id = Guid.NewGuid(),
            LeagueId = score.LeagueId,
            GameId = score.GameId,
            Kind = kind,
            At = at,
            ByUserId = byUserId,
            ByPlayerId = byPlayerId,
            Before = before,
            After = after,
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ScoreChanged();
        }

        return null;
    }
}
