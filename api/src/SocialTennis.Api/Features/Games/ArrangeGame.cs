using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Access;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Games.Contracts;

namespace SocialTennis.Api.Features.Games;

/// <summary>
/// A Player in a League Game sets the Date its Players agreed, and may note
/// the Court (#9). Any of them may, and the last change stands (owner,
/// 2026-11-27). Courts are the Players' business (ADR-0006): the note is
/// theirs, and a Manager doesn't set either.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A Player the signed-in User doesn't hold, or a Game they can't see: 404, as for its Match screen.</item>
/// <item>The User's Player, but not in this Game: 403 <c>not-in-game</c>.</item>
/// <item>A Game with a Score (it's been played), one marked not played, or one whose Stage has closed:
/// 409 <c>already-scored</c>, <c>not-played</c> or <c>stage-closed</c>.</item>
/// <item>A Date before its Stage starts or after its League ends: 400 under <c>date</c>. A Stage held up past
/// its deadline is still open, so a Date after the Stage's end is fine.</item>
/// </list>
/// It holds the League's results lock, as entering a Score does, so it can't land on a Game a close or a
/// Manager's move is taking away.
/// </remarks>
public static class ArrangeGame
{
    public static async Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> HandleAsync(
        Guid gameId,
        ArrangeGameRequest request,
        ClaimsPrincipal user,
        TennisDbContext db,
        ManagerAccess access,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        if (!await db.Players.AnyAsync(p => p.Id == request.PlayerId && p.UserId == userId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

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
                g.Stage.StartDate,
                LeagueEndDate = g.Stage.League.EndDate,
                Closed = g.Stage.ClosedAt != null,
                InLeague = g.Stage.League.Participants.Any(p => p.Player.UserId == userId),
                Plays = g.Sides.Any(s => s.Team.Members.Any(m => m.PlayerId == request.PlayerId)),
                Scored = g.Score != null,
                Voided = g.VoidedAt != null,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (game is null || !(game.InLeague || await access.RunsLeagueAsync(user, game.LeagueId, cancellationToken)))
        {
            return TypedResults.NotFound();
        }

        if (!game.Plays)
        {
            return GameScores.NotInGame();
        }

        if (game.Closed)
        {
            return GameScores.StageClosed();
        }

        if (game.Voided)
        {
            return GameScores.NotPlayed();
        }

        if (game.Scored)
        {
            return GameScores.AlreadyScored();
        }

        if (request.Date is { } date && (date < game.StartDate || date > game.LeagueEndDate))
        {
            var format = (DateOnly day) => day.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["date"] = [$"Pick a day from {format(game.StartDate)}, when the Stage starts, to {format(game.LeagueEndDate)}, when the League ends."],
            });
        }

        var court = request.Court?.Trim();
        await db.Games
            .Where(g => g.Id == gameId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(g => g.Date, request.Date)
                    .SetProperty(g => g.Court, string.IsNullOrEmpty(court) ? null : court)
                    .SetProperty(g => g.DateSetById, request.Date is null ? (Guid?)null : request.PlayerId),
                cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
