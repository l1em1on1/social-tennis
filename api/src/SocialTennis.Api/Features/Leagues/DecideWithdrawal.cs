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
/// The League's Manager accepts or declines a withdrawal request (#21).
/// Accepting marks the Team Withdrawn: it stays whole and in its place until
/// the close, so its past Games keep their names; its Games with no score
/// stay on the to-do, for the Manager to mark not played, and those don't
/// cost its opponents the bonus point. Declining changes nothing else.
/// </summary>
/// <remarks>
/// Managers only, by the routing table's policy. No such request in the League: 404. Already decided:
/// 409 <c>already-decided</c>. The League has ended: 409 <c>stage-closed</c>. Holds the League's entry and
/// results locks, so no Score or close lands in the middle of it.
/// </remarks>
public static class DecideWithdrawal
{
    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
        Guid requestId,
        DecideWithdrawalRequest decision,
        ClaimsPrincipal user,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        await using var transaction = await LeagueEntry.LockLeagueAsync(db, leagueId, cancellationToken);
        await GameScores.TakeResultsLockAsync(db, leagueId, cancellationToken);
        var request = await db.WithdrawalRequests.Include(r => r.Team).SingleOrDefaultAsync(r => r.Id == requestId && r.LeagueId == leagueId, cancellationToken);
        if (request is null)
        {
            return TypedResults.NotFound();
        }

        if (request.Status != WithdrawalStatus.Pending)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, type: "already-decided", title: "This request has already been decided.");
        }

        if (decision.Accept && await Withdrawals.OpenStageWithAsync(db, leagueId, request.TeamId, cancellationToken) is null)
        {
            return StageClose.AlreadyClosed();
        }

        var now = clock.GetUtcNow();
        request.Status = decision.Accept ? WithdrawalStatus.Accepted : WithdrawalStatus.Declined;
        request.DecidedAt = now;
        request.DecidedByUserId = user.UserId();
        if (decision.Accept)
        {
            request.Team.WithdrawnAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
