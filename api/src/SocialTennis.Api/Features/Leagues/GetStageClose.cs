using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// The close screen (#10, wireframe M6): the current Stage's Divisions and
/// Standings, what waits before it can close, the Teams waiting for a place,
/// and the next Stage's proposed dates.
/// </summary>
/// <remarks>
/// Managers only, by the routing table's policy. No League, or one not
/// started: 404. A League whose last Stage has closed has ended: 409 <c>stage-closed</c>.
/// </remarks>
public static class GetStageClose
{
    public static async Task<Results<Ok<GetStageCloseResponse>, NotFound, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var league = await db.Leagues
            .Where(l => l.Id == leagueId)
            .Select(l => new
            {
                l.Type,
                l.EndDate,
                l.StageLength,
                l.ScoreFormat,
                l.StandingsMode,
                l.Club.TimeZone,
                Stage = l.Stages.OrderByDescending(s => s.Number).Select(s => new { s.Id, s.Number, s.StartDate, s.EndDate, Closed = s.ClosedAt != null }).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (league?.Stage is not { } stage)
        {
            return TypedResults.NotFound();
        }

        if (stage.Closed)
        {
            return StageClose.AlreadyClosed();
        }

        var (divisions, awaiting) = await StageLadder.BuildAsync(db, stage.Id, league.ScoreFormat, league.Type, league.StandingsMode, cancellationToken);
        var next = StageDates.Next(stage.EndDate, league.EndDate, league.StageLength);
        return TypedResults.Ok(new GetStageCloseResponse(
            new LeagueStageSummary(stage.Number, stage.StartDate, stage.EndDate),
            league.EndDate,
            StageDates.DeadlinePassed(stage.EndDate, league.TimeZone, clock),
            divisions,
            awaiting,
            next is null ? [] : await StageClose.WaitingAsync(db, leagueId, stage.Id, league.Type, cancellationToken),
            next is { } dates ? new CloseNextStage(dates.Start, dates.End) : null,
            next is null ? [] : await StageClose.OffLadderAsync(db, leagueId, stage.Id, league.Type, cancellationToken)));
    }
}
