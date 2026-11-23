using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues;

namespace SocialTennis.Api.Jobs;

/// <summary>
/// Closes every auto-rolling League's Stage once its deadline has passed and
/// nothing waits on the Manager (#10; ADR-0016, ADR-0019): its Standings
/// decide the moves, and the next Stage opens on its default dates, with no
/// Team added. A Stage with a Game nobody scored, or a tie that decides who
/// moves, stays open and held up: the Manager settles it and closes it by hand.
/// </summary>
/// <remarks>
/// Idempotent: a Stage closes once, so running the job twice, or late, does
/// the same. It closes through <see cref="StageClose.CloseAsync"/>, as the
/// Manager's close does, under the same locks.
/// </remarks>
public sealed class AutoRollJob(TennisDbContext db, TimeProvider clock, ILogger<AutoRollJob> logger) : IJob
{
    public string Name => "auto-roll";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var candidates = await db.Leagues
            .Where(l => l.Structure == LeagueStructure.Divisional && l.StageRoll == StageRoll.Automatic)
            .Select(l => new
            {
                l.Id,
                l.EndDate,
                l.StageLength,
                l.ScoreFormat,
                l.Club.TimeZone,
                Stage = l.Stages.OrderByDescending(s => s.Number).Select(s => new { s.Id, s.EndDate, Closed = s.ClosedAt != null }).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var (closed, held) = (0, 0);
        foreach (var league in candidates)
        {
            if (league.Stage is not { Closed: false } stage || !StageDates.DeadlinePassed(stage.EndDate, league.TimeZone, clock))
            {
                continue;
            }

            var (divisions, awaiting) = await StageLadder.BuildAsync(db, stage.Id, league.ScoreFormat, cancellationToken);
            if (awaiting.UnscoredGameIds.Count > 0 || awaiting.Ties.Count > 0)
            {
                held++;
                continue;
            }

            var next = StageDates.Next(stage.EndDate, league.EndDate, league.StageLength);
            var result = await StageClose.CloseAsync(
                db,
                league.Id,
                [.. divisions.Select(d => (IReadOnlyList<Guid>)[.. d.Standings.Select(s => s.TeamId)])],
                [],
                next?.Start,
                next?.End,
                clock,
                cancellationToken,
                ordersTies: false);
            if (result.Refusal == CloseRefusal.None)
            {
                closed++;
            }
            else
            {
                // A Score came in between, or a Manager closed it first: the next run sees how it stands.
                logger.LogInformation("League {LeagueId}'s Stage didn't roll: {Refusal}.", league.Id, result.Refusal);
            }

            db.ChangeTracker.Clear();
        }

        logger.LogInformation("Rolled {Closed} Stages; {Held} held up, waiting on their Managers.", closed, held);
    }
}
