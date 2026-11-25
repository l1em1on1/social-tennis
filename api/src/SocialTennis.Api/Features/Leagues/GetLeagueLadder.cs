using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Access;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A League's ladder in its current Stage (#7): the Divisions from the top,
/// each with its Teams and every Game between them (ADR-0031), with its Score
/// once entered (#38), and its Standings from those Scores (#10). The Players in
/// the League see it, whether or not they are on the ladder, and so do Managers.
/// </summary>
/// <remarks>
/// Anyone else gets a 404, as if the League weren't there (ADR-0017): a Player
/// who isn't in the League can't see its ladder or fixtures. Before the start
/// the ladder is empty and <c>stage</c> is null.
/// </remarks>
public static class GetLeagueLadder
{
    public static async Task<Results<Ok<GetLeagueLadderResponse>, NotFound>> HandleAsync(
        Guid leagueId,
        ClaimsPrincipal user,
        TennisDbContext db,
        ManagerAccess access,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        var league = await db.Leagues
            .Where(l => l.Id == leagueId)
            .Select(l => new
            {
                l.Name,
                ClubName = l.Club.Name,
                l.Type,
                l.ScoreFormat,
                l.Club.TimeZone,
                Yours = l.Participants.Where(p => p.Player.UserId == userId).Select(p => p.PlayerId).ToList(),
                Stage = l.Stages.OrderByDescending(s => s.Number).Select(s => new { s.Id, s.Number, s.StartDate, s.EndDate, Closed = s.ClosedAt != null }).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);
        var manages = league is not null && await access.RunsLeagueAsync(user, leagueId, cancellationToken);
        if (league is null || !(league.Yours.Count > 0 || manages))
        {
            return TypedResults.NotFound();
        }

        var managers = await LeagueManagerList.NamesAsync(db, leagueId, cancellationToken);

        if (league.Stage is null)
        {
            return TypedResults.Ok(new GetLeagueLadderResponse(
                leagueId, league.Name, league.ClubName, league.Type, null, [], false, new LadderAwaiting([], []), false, league.Yours, managers, manages));
        }

        var (divisions, awaiting) = await StageLadder.BuildAsync(db, league.Stage.Id, league.ScoreFormat, cancellationToken);
        if (league.Stage.Closed)
        {
            awaiting = new LadderAwaiting([], []);
        }

        return TypedResults.Ok(new GetLeagueLadderResponse(
            leagueId,
            league.Name,
            league.ClubName,
            league.Type,
            new LeagueStageSummary(league.Stage.Number, league.Stage.StartDate, league.Stage.EndDate),
            divisions,
            !league.Stage.Closed && StageDates.DeadlinePassed(league.Stage.EndDate, league.TimeZone, clock),
            awaiting,
            league.Stage.Closed,
            league.Yours,
            managers,
            manages));
    }
}
