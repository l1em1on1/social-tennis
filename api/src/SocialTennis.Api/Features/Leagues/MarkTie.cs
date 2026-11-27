using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Games;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Foursomes Player marks who goes up, or down, of the Players level on games
/// at a place that moves in their Division (#14 part 3; owner, 2026-11-26: the
/// Players decide, a coin toss or otherwise, and say so with the scores). The
/// last answer stands. It holds while the same Players are level there; the
/// Manager can still order the tie at the close.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A Player the signed-in User doesn't hold, or a League none of their Players is in: 404, as for its ladder.</item>
/// <item>A Player not on the ladder in that Division: 403 <c>not-in-division</c>. A Manager orders ties at the close.</item>
/// <item>The League has ended: 409 <c>stage-closed</c>.</item>
/// <item>Nobody level there that the Players settle: 409 <c>no-tie-to-mark</c>. That's before every Game in
/// the Division has a Score, once a Game wasn't played (the Manager decides), or once a Score changed the tie.</item>
/// <item>A Team not level there, or the one already marked to go the other way: 400 under <c>teamId</c>.</item>
/// </list>
/// It holds the League's entry and results locks, as closing does, so no Score
/// or close lands between reading the tie and marking it.
/// </remarks>
public static class MarkTie
{
    public static async Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
        MarkTieRequest request,
        ClaimsPrincipal user,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        if (!await db.LeagueParticipants.AnyAsync(
                p => p.LeagueId == leagueId && p.PlayerId == request.PlayerId && p.Player.UserId == userId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        await using var transaction = await LeagueEntry.LockLeagueAsync(db, leagueId, cancellationToken);
        await GameScores.TakeResultsLockAsync(db, leagueId, cancellationToken);
        var league = await db.Leagues
            .Where(l => l.Id == leagueId)
            .Select(l => new
            {
                l.Type,
                l.ScoreFormat,
                l.StandingsMode,
                Stage = l.Stages.OrderByDescending(s => s.Number).Select(s => new { s.Id, Closed = s.ClosedAt != null }).FirstOrDefault(),
            })
            .SingleAsync(cancellationToken);
        if (league.Stage is not { } stage)
        {
            return NoTie();
        }

        if (stage.Closed)
        {
            return StageClose.AlreadyClosed();
        }

        var (divisions, _) = await StageLadder.BuildAsync(db, stage.Id, league.ScoreFormat, league.Type, league.StandingsMode, cancellationToken);
        var division = divisions.SingleOrDefault(d => d.Number == request.Division);
        if (division is null || !division.Competitors.Any(c => c.Players.Any(p => p.PlayerId == request.PlayerId)))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                type: "not-in-division",
                title: "Only the Division's own Players say who goes.",
                detail: "The Manager orders a tie when closing the Stage.");
        }

        if (division.PlayersTies.SingleOrDefault(t => t.Place == request.Place) is not { } tie)
        {
            return NoTie();
        }

        var other = division.PlayersTies.SingleOrDefault(t => t.Place != request.Place && t.TeamIds.ToHashSet().SetEquals(tie.TeamIds));
        if (!tie.TeamIds.Contains(request.TeamId) || other?.Mark?.TeamId == request.TeamId)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["teamId"] = [other?.Mark?.TeamId == request.TeamId
                    ? "They're already marked to go the other way."
                    : "Pick one of the Players level there."],
            });
        }

        var mark = await db.TieMarks.SingleOrDefaultAsync(
            m => m.StageId == stage.Id && m.Division == request.Division && m.Place == request.Place, cancellationToken);
        if (mark is null)
        {
            mark = new TieMark { StageId = stage.Id, LeagueId = leagueId, Division = request.Division, Place = request.Place };
            db.TieMarks.Add(mark);
        }

        mark.Group = [.. tie.TeamIds.Order()];
        mark.TeamId = request.TeamId;
        mark.MarkedById = request.PlayerId;
        mark.MarkedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static ProblemHttpResult NoTie() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "no-tie-to-mark",
            title: "Nobody is level there for the Players to settle.",
            detail: "A tie is the Players' to mark once every Game in the Division has a score and none was not played. The ladder shows what's open.");
}
