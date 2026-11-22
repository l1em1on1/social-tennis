using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Games;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Manager closes the current Stage (#10; see CONTEXT.md: Stage,
/// Relegation): it locks for its Players, Relegation moves each Division's
/// first up and last down (<see cref="Relegation.Next"/>), the Teams the
/// Manager adds join the bottom, and the next Stage opens with a Game for
/// every pair in each Division. Closing the League's last Stage ends it, and
/// nobody moves.
/// </summary>
/// <remarks>
/// <para>
/// The request says what the Manager confirmed: every Division in its final
/// order. That order must still follow the Standings, with ties in any order
/// the Manager chose. A Score entered or changed since, so that it no longer
/// does, is a 409 <c>standings-changed</c>, and the Teams joining must still
/// be waiting (409 <c>league-changed</c>), so nobody moves on a result the
/// Manager didn't see. The close holds the League's entry lock and its
/// results lock, so no Score lands between the check and the close.
/// </para>
/// <list type="bullet">
/// <item>No League, or one not started: 404. Its last Stage already closed: 409 <c>stage-closed</c>.</item>
/// <item>A Game with no Score that isn't marked not played: 409 <c>stage-queue-not-empty</c> (ADR-0017).</item>
/// <item>The next Stage's dates: from after this Stage ends to the League's end, or 400 under their wire name.
/// None, and no Teams joining, when this is the League's last Stage.</item>
/// </list>
/// </remarks>
public static class CloseStage
{
    public static async Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
        CloseStageRequest request,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        await using var transaction = await LeagueEntry.LockLeagueAsync(db, leagueId, cancellationToken);
        await GameScores.TakeResultsLockAsync(db, leagueId, cancellationToken);
        var league = await db.Leagues
            .Where(l => l.Id == leagueId)
            .Select(l => new { l.Type, l.EndDate, l.StageLength, l.ScoreFormat })
            .SingleOrDefaultAsync(cancellationToken);
        var stage = await db.Stages
            .Where(s => s.LeagueId == leagueId)
            .OrderByDescending(s => s.Number)
            .FirstOrDefaultAsync(cancellationToken);
        if (league is null || stage is null)
        {
            return TypedResults.NotFound();
        }

        if (stage.ClosedAt is not null)
        {
            return StageClose.AlreadyClosed();
        }

        var (divisions, awaiting) = await StageLadder.BuildAsync(db, stage.Id, league.ScoreFormat, cancellationToken);
        if (awaiting.UnscoredGameIds.Count > 0)
        {
            return StageClose.QueueNotEmpty();
        }

        if (!FollowsStandings(divisions, request.Divisions))
        {
            return StageClose.StandingsChanged();
        }

        var next = StageDates.Next(stage.EndDate, league.EndDate, league.StageLength);
        var waiting = next is null ? [] : await StageClose.WaitingAsync(db, leagueId, stage.Id, league.Type, cancellationToken);
        if (request.Joining.Distinct().Count() != request.Joining.Count
            || !request.Joining.All(team => waiting.Any(w => w.TeamId == team)))
        {
            return StageClose.LeagueChanged();
        }

        var errors = new Dictionary<string, string[]>();
        if (next is null && request.NextStartDate is not null)
        {
            errors["nextStartDate"] = ["This is the League's last Stage: closing it ends the League."];
        }
        else if (next is not null && request.NextStartDate is not { } nextStart)
        {
            errors["nextStartDate"] = ["Give the next Stage its dates."];
        }
        else if (request.NextStartDate is { } start && request.NextEndDate is { } end)
        {
            if (start <= stage.EndDate)
            {
                errors["nextStartDate"] = [$"The next Stage starts after this one ends: on {stage.EndDate.AddDays(1).ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)} or later."];
            }

            if (end > league.EndDate)
            {
                errors["nextEndDate"] = [$"The League ends on {league.EndDate.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)}."];
            }
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var now = clock.GetUtcNow();
        stage.ClosedAt = now;
        if (request.NextStartDate is { } nextStartDate && request.NextEndDate is { } nextEndDate)
        {
            var opened = new Stage
            {
                Id = Guid.NewGuid(),
                LeagueId = leagueId,
                Number = stage.Number + 1,
                StartDate = nextStartDate,
                EndDate = nextEndDate,
                OpenedAt = now,
            };
            db.Stages.Add(opened);
            StageLadder.Place(opened, leagueId, Relegation.Next(request.Divisions, request.Joining));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Whether the confirmed order is every Division's Teams, and still follows
    /// its Standings: no Team after one ranked below it. Tied Teams may be in
    /// any order: the Manager's.
    /// </summary>
    private static bool FollowsStandings(IReadOnlyList<LadderDivision> divisions, IReadOnlyList<IReadOnlyList<Guid>> confirmed)
    {
        if (confirmed.Count != divisions.Count)
        {
            return false;
        }

        for (var index = 0; index < divisions.Count; index++)
        {
            var rankOf = divisions[index].Standings.ToDictionary(s => s.TeamId, s => s.Rank);
            var order = confirmed[index];
            if (order.Count != rankOf.Count || order.Distinct().Count() != order.Count || !order.All(rankOf.ContainsKey))
            {
                return false;
            }

            for (var place = 1; place < order.Count; place++)
            {
                if (rankOf[order[place]] < rankOf[order[place - 1]])
                {
                    return false;
                }
            }
        }

        return true;
    }
}
