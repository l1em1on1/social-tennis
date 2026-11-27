using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Games;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>What closing a Stage reads, for its screen and for the close itself (#10).</summary>
public static class StageClose
{
    /// <summary>
    /// Teams in the League but off the current ladder, complete for its Type
    /// (a doubles Team of two), which the Manager can add at close. None in
    /// Foursomes (#14): nobody joins a running ladder (owner, 2026-11-27). A
    /// Player leaving for good is replaced in their place instead (#21).
    /// </summary>
    public static async Task<List<CloseWaitingTeam>> WaitingAsync(
        TennisDbContext db, Guid leagueId, Guid stageId, LeagueType type, CancellationToken cancellationToken)
    {
        if (type == LeagueType.Foursomes)
        {
            return [];
        }

        var size = type.TeamSize();
        return await db.Teams
            .Where(t => t.LeagueId == leagueId && t.Members.Count == size && !db.LadderPlaces.Any(p => p.StageId == stageId && p.TeamId == t.Id))
            .OrderBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Select(t => new CloseWaitingTeam(
                t.Id,
                t.Members.OrderBy(m => m.JoinedAt).ThenBy(m => m.PlayerId).Select(m => new LadderPlayer(m.PlayerId, m.Player.Name)).ToList()))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public static ProblemHttpResult AlreadyClosed() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "stage-closed",
            title: "This League's last Stage has closed: the League has ended.");

    /// <summary>A Team that has withdrawn is still on the ladder (#21): the close that takes it off is still to come.</summary>
    public static ProblemHttpResult WithdrawnOnLadder() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "withdrawn-on-ladder",
            title: "A Team that has withdrawn is still on the ladder.",
            detail: "Closing a Stage with one, and naming a replacement, is still to be built.");

    /// <summary>ADR-0017's own example: a Stage can't close while anything waits on the Manager.</summary>
    public static ProblemHttpResult QueueNotEmpty() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "stage-queue-not-empty",
            title: "Games with no score are holding the Stage up.",
            detail: "Record each one's score, or mark it not played, then close the Stage.");

    public static ProblemHttpResult StandingsChanged() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "standings-changed",
            title: "The Standings have changed since the close screen loaded.",
            detail: "A score was entered or changed in the meantime. Look at the close again.");

    public static ProblemHttpResult LeagueChanged() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "league-changed",
            title: "The Teams waiting for a place have changed since the close screen loaded.",
            detail: "Look at the close again.");

    /// <summary>
    /// Closes the League's current Stage with <paramref name="order"/>, every
    /// Division's final order as the Manager confirmed it (or, for auto-roll,
    /// its Standings), <paramref name="joining"/> Teams added at the bottom,
    /// and the next Stage's dates: what <see cref="CloseStage"/> describes,
    /// for the endpoint and the auto-roll job alike. The job passes
    /// <paramref name="ordersTies"/> false: a tie that decides who moves is the
    /// Manager's to order, so it refuses one, checked under the locks.
    /// </summary>
    public static async Task<CloseResult> CloseAsync(
        TennisDbContext db,
        Guid leagueId,
        IReadOnlyList<IReadOnlyList<Guid>> order,
        IReadOnlyList<Guid> joining,
        DateOnly? nextStartDate,
        DateOnly? nextEndDate,
        TimeProvider clock,
        CancellationToken cancellationToken,
        bool ordersTies = true)
    {
        await using var transaction = await LeagueEntry.LockLeagueAsync(db, leagueId, cancellationToken);
        await GameScores.TakeResultsLockAsync(db, leagueId, cancellationToken);
        var league = await db.Leagues
            .Where(l => l.Id == leagueId)
            .Select(l => new { l.Type, l.EndDate, l.StageLength, l.ScoreFormat, l.StandingsMode })
            .SingleOrDefaultAsync(cancellationToken);
        var stage = await db.Stages
            .Where(s => s.LeagueId == leagueId)
            .OrderByDescending(s => s.Number)
            .FirstOrDefaultAsync(cancellationToken);
        if (league is null || stage is null)
        {
            return new CloseResult(CloseRefusal.NotFound);
        }

        if (stage.ClosedAt is not null)
        {
            return new CloseResult(CloseRefusal.AlreadyClosed);
        }

        var (divisions, awaiting) = await StageLadder.BuildAsync(db, stage.Id, league.ScoreFormat, league.Type, league.StandingsMode, cancellationToken);
        if (awaiting.UnscoredGameIds.Count > 0)
        {
            return new CloseResult(CloseRefusal.QueueNotEmpty);
        }

        if (!ordersTies && awaiting.Ties.Count > 0)
        {
            return new CloseResult(CloseRefusal.TiesOpen);
        }

        // A Team that has withdrawn leaves the ladder at the close, or a replacement takes its place (#21, part 2).
        // Until that is built, the close waits rather than carry it into the next Stage.
        if (divisions.Any(d => d.Competitors.Any(c => c.Withdrawn)))
        {
            return new CloseResult(CloseRefusal.WithdrawnOnLadder);
        }

        // The automatic close orders no tie of its own: its order must be the Standings' exactly, as rebuilt
        // under the locks, so a Players' mark changed since it read the ladder (#14 part 3) can't be passed over.
        var follows = ordersTies
            ? FollowsStandings(divisions, order)
            : divisions.Count == order.Count && divisions.Select((d, i) => d.Standings.Select(s => s.TeamId).SequenceEqual(order[i])).All(same => same);
        if (!follows)
        {
            return new CloseResult(CloseRefusal.StandingsChanged);
        }

        var next = StageDates.Next(stage.EndDate, league.EndDate, league.StageLength);
        var waiting = next is null ? [] : await WaitingAsync(db, leagueId, stage.Id, league.Type, cancellationToken);
        if (joining.Distinct().Count() != joining.Count
            || !joining.All(team => waiting.Any(w => w.TeamId == team)))
        {
            return new CloseResult(CloseRefusal.LeagueChanged);
        }

        var errors = new Dictionary<string, string[]>();
        if (next is null && nextStartDate is not null)
        {
            errors["nextStartDate"] = ["This is the League's last Stage: closing it ends the League."];
        }
        else if (next is not null && nextStartDate is null)
        {
            errors["nextStartDate"] = ["Give the next Stage its dates."];
        }
        else if (nextStartDate is { } start && nextEndDate is { } end)
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
            return new CloseResult(CloseRefusal.InvalidDates, errors);
        }

        var now = clock.GetUtcNow();
        stage.ClosedAt = now;
        if (nextStartDate is { } opensOn && nextEndDate is { } endsOn)
        {
            var opened = new Stage
            {
                Id = Guid.NewGuid(),
                LeagueId = leagueId,
                Number = stage.Number + 1,
                StartDate = opensOn,
                EndDate = endsOn,
                OpenedAt = now,
            };
            db.Stages.Add(opened);
            StageLadder.Place(opened, leagueId, league.Type, Relegation.Next(order, joining));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CloseResult(CloseRefusal.None);
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

/// <summary>Why a close was refused, if it was (<see cref="StageClose.CloseAsync"/>).</summary>
public enum CloseRefusal
{
    None,
    NotFound,
    AlreadyClosed,
    QueueNotEmpty,
    /// <summary>A tie decides who moves, and the auto-roll job doesn't order ties.</summary>
    TiesOpen,
    StandingsChanged,
    LeagueChanged,
    InvalidDates,
    WithdrawnOnLadder,
}

/// <summary>A close's outcome: refused or not, and the dates' problems by wire name.</summary>
public sealed record CloseResult(CloseRefusal Refusal, Dictionary<string, string[]>? Errors = null);
