using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Games.Contracts;

namespace SocialTennis.Api.Features.Games;

/// <summary>
/// A Player who played a Game corrects its Score while its Stage is open
/// (#38; ADR-0008, Phasing): either Side, whoever entered it. The change is
/// kept in the Score's history, with the old value, for the League to see.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A Player the User doesn't hold, or a Game they can't see: 404, as for entering one.</item>
/// <item>Their Player not in this Game: 403 <c>not-in-game</c>.</item>
/// <item>No Score yet: 409 <c>not-scored</c>; enter one instead.</item>
/// <item>The Stage has closed: 409 <c>stage-closed</c>. Only a Manager's override changes it then.</item>
/// <item>A correction made from an older version than the Score's: 409 <c>score-changed</c>, and nothing
/// changes (ADR-0018). The web app shows the Score as it is now, to make the change again.</item>
/// <item>The format and date rules of entering one: 400 under the wire name.</item>
/// </list>
/// A correction that says what the Score already does changes nothing and records nothing.
/// </remarks>
public static class CorrectScore
{
    public static async Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> HandleAsync(
        Guid gameId,
        CorrectScoreRequest request,
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
                Closed = g.Stage.ClosedAt != null,
                g.Stage.League.Club.TimeZone,
                g.Stage.League.ScoreFormat,
                InLeague = g.Stage.League.Participants.Any(p => p.Player.UserId == userId),
                Plays = g.Sides.Any(s => s.Team.Members.Any(m => m.PlayerId == request.PlayerId)),
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

        var score = await db.Scores.SingleOrDefaultAsync(s => s.LeagueId == game.LeagueId && s.GameId == gameId, cancellationToken);
        if (score is null)
        {
            return GameScores.NotScored();
        }

        if (game.Closed)
        {
            return GameScores.StageClosed();
        }

        var today = ClubCalendar.Today(game.TimeZone, clock);
        var (winner, errors) = GameScores.Check(game.ScoreFormat, game.Number, game.StartDate, today, request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (await GameScores.ChangeAsync(
                db, score, request, request.Version, winner, ScoreChangeKind.Corrected, clock.GetUtcNow(), userId, request.PlayerId, cancellationToken) is { } refused)
        {
            return refused;
        }

        return TypedResults.NoContent();
    }
}
