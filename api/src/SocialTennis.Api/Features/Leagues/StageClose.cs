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
        TennisDbContext db, Guid leagueId, Guid stageId, LeagueType type, CancellationToken cancellationToken) =>
        type == LeagueType.Foursomes ? [] : await OffLadderAsync(db, leagueId, stageId, type, cancellationToken);

    /// <summary>
    /// Teams in the League, complete for its Type and off the current ladder, that haven't withdrawn:
    /// those who can take a withdrawn Team's place at the close (#21), in Foursomes too.
    /// </summary>
    public static async Task<List<CloseWaitingTeam>> OffLadderAsync(
        TennisDbContext db, Guid leagueId, Guid stageId, LeagueType type, CancellationToken cancellationToken)
    {
        var size = type == LeagueType.Foursomes ? 1 : type.TeamSize();
        return await db.Teams
            .Where(t => t.LeagueId == leagueId && t.WithdrawnAt == null && t.Members.Count == size
                && !db.LadderPlaces.Any(p => p.StageId == stageId && p.TeamId == t.Id))
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
        bool ordersTies = true,
        IReadOnlyList<CloseReplacement>? replacements = null)
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

        // Withdrawn Teams (#21) leave the ladder, or a Team off it takes each one's place.
        var withdrawn = divisions.SelectMany(d => d.Competitors).Where(c => c.Withdrawn).Select(c => c.TeamId).ToHashSet();
        var replacing = (replacements ?? []).ToDictionary(r => r.TeamId, r => r.ReplacementTeamId);
        if ((replacements ?? []).Count != replacing.Count
            || replacing.Keys.Any(team => !withdrawn.Contains(team))
            || replacing.Values.Distinct().Count() != replacing.Count)
        {
            return new CloseResult(CloseRefusal.InvalidReplacements, new Dictionary<string, string[]>
            {
                ["replacements"] = ["Replace only a Team that has withdrawn, each once, and with a different Team each time."],
            });
        }

        if (next is not null && replacing.Count > 0)
        {
            var candidates = await OffLadderAsync(db, leagueId, stage.Id, league.Type, cancellationToken);
            if (replacing.Values.Any(team => joining.Contains(team) || !candidates.Any(c => c.TeamId == team)))
            {
                return new CloseResult(CloseRefusal.LeagueChanged);
            }
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
            // Who moves is worked out with the withdrawn Teams still in place, so their leaving moves nobody
            // else; then each one's landing place goes to its replacement, or goes (#21).
            var placed = Relegation.Next(order, joining)
                .Select(division => (IReadOnlyList<Guid>)[
                    .. division
                        .Where(team => !withdrawn.Contains(team) || replacing.ContainsKey(team))
                        .Select(team => replacing.TryGetValue(team, out var replacement) ? replacement : team),
                ])
                .ToList();
            List<Guid> missing = league.Type == LeagueType.Foursomes ? [.. withdrawn.Where(team => !replacing.ContainsKey(team))] : [];
            var small = placed.Select((division, index) => (division, index)).FirstOrDefault(d => d.division.Count < League.MinDivisionSize);
            if (missing.Count > 0 || small.division is not null)
            {
                return new CloseResult(CloseRefusal.InvalidReplacements, new Dictionary<string, string[]>
                {
                    ["replacements"] = [missing.Count > 0
                        ? "A Foursomes Division is always 4: name a replacement for each Player who has withdrawn."
                        : $"Division {small.index + 1} would be left with one Team: name a replacement for the Team that has withdrawn."],
                });
            }

            StageLadder.Place(opened, leagueId, league.Type, placed);
        }
        else
        {
            // The League has ended (#21): a request to withdraw still waiting means nothing now, so it goes,
            // as if taken back, rather than wait on the Manager for good.
            await db.WithdrawalRequests
                .Where(r => r.LeagueId == leagueId && r.Status == WithdrawalStatus.Pending)
                .ExecuteDeleteAsync(cancellationToken);
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
    /// <summary>A replacement for a withdrawn Team that doesn't fit (#21).</summary>
    InvalidReplacements,
}

/// <summary>A close's outcome: refused or not, and the dates' problems by wire name.</summary>
public sealed record CloseResult(CloseRefusal Refusal, Dictionary<string, string[]>? Errors = null);
