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
/// A Player who played a League Game enters its Score (#38): the Date played
/// and its Sets, or a Retirement with a declared winner. It records who
/// entered it, and its history starts with that entry.
/// </summary>
/// <remarks>
/// Who may, and what answers otherwise:
/// <list type="bullet">
/// <item>A Player the signed-in User doesn't hold: 404.</item>
/// <item>A Game the User can't see (none of their Players is in its League, and they aren't a Manager): 404, as for its Match screen.</item>
/// <item>The User's Player, in the League or a Manager, but not in this Game: 403 <c>not-in-game</c>. A Manager overriding a Score is #38's second part.</item>
/// <item>A Game that has a Score: 409 <c>already-scored</c>, also when two are entered at once.</item>
/// <item>Sets the League's format doesn't allow (<see cref="ScoreRules"/>), or a Date played before the
/// Stage started or after today in the Club's time zone: 400, with the problem under its wire name
/// (ADR-0017), so the form shows it where it belongs.</item>
/// </list>
/// </remarks>
public static class SubmitScore
{
    public static async Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> HandleAsync(
        Guid gameId,
        SubmitScoreRequest request,
        ClaimsPrincipal user,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        if (!await db.Players.AnyAsync(p => p.Id == request.PlayerId && p.UserId == userId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var game = await db.Games
            .Where(g => g.Id == gameId)
            .Select(g => new
            {
                g.LeagueId,
                g.Stage.Number,
                g.Stage.StartDate,
                g.Stage.League.Club.TimeZone,
                g.Stage.League.ScoreFormat,
                InLeague = g.Stage.League.Participants.Any(p => p.Player.UserId == userId),
                Plays = g.Sides.Any(s => s.Team.Members.Any(m => m.PlayerId == request.PlayerId)),
                Scored = g.Score != null,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (game is null || !(game.InLeague || user.IsInRole(Policies.Manager)))
        {
            return TypedResults.NotFound();
        }

        if (!game.Plays)
        {
            return GameScores.NotInGame();
        }

        if (game.Scored)
        {
            return GameScores.AlreadyScored();
        }

        var errors = new Dictionary<string, string[]>();
        var check = ScoreRules.Check(game.ScoreFormat, request.Sets, request.RetirementWinner);
        if (!check.IsValid)
        {
            errors[check.Field!] = [check.Message!];
        }

        var today = ClubCalendar.Today(game.TimeZone, clock);
        if (request.PlayedOn < game.StartDate)
        {
            errors["playedOn"] = [$"Stage {game.Number} started on {game.StartDate:d MMM yyyy}: the game can't have been played before that."];
        }
        else if (request.PlayedOn > today)
        {
            errors["playedOn"] = ["The date played can't be in the future."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var now = clock.GetUtcNow();
        var score = new Score
        {
            GameId = gameId,
            LeagueId = game.LeagueId,
            PlayedOn = request.PlayedOn,
            Sets = [.. request.Sets],
            Winner = check.Winner,
            Retired = request.RetirementWinner is not null,
            SubmittedById = request.PlayerId,
            SubmittedAt = now,
        };
        db.Scores.Add(score);
        db.ScoreChanges.Add(new ScoreChange
        {
            Id = Guid.NewGuid(),
            LeagueId = game.LeagueId,
            GameId = gameId,
            Kind = ScoreChangeKind.Entered,
            At = now,
            ByPlayerId = request.PlayerId,
            After = ScoreValue.Of(score),
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another Player entered it between the check above and this save: the Score is keyed by its Game.
            return GameScores.AlreadyScored();
        }

        return TypedResults.NoContent();
    }
}
