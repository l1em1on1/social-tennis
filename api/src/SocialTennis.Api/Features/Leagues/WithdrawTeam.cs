using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Games;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// The League's Manager withdraws a Team directly, without a request (#21;
/// owner, 2026-11-27), as accepting one would. A request already waiting for
/// it is accepted; otherwise an accepted one is recorded that nobody asked
/// for, so the Players see what happened.
/// </summary>
/// <remarks>
/// Managers only, by the routing table's policy. No such Team in the League: 404. Off the running ladder:
/// 409 <c>not-on-the-ladder</c>. Already withdrawn: 409 <c>already-withdrawn</c>.
/// </remarks>
public static class WithdrawTeam
{
    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
        Guid teamId,
        ClaimsPrincipal user,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        await using var transaction = await LeagueEntry.LockLeagueAsync(db, leagueId, cancellationToken);
        await GameScores.TakeResultsLockAsync(db, leagueId, cancellationToken);
        var team = await db.Teams.SingleOrDefaultAsync(t => t.Id == teamId && t.LeagueId == leagueId, cancellationToken);
        if (team is null)
        {
            return TypedResults.NotFound();
        }

        if (team.WithdrawnAt is not null)
        {
            return Withdrawals.AlreadyWithdrawn();
        }

        if (await Withdrawals.OpenStageWithAsync(db, leagueId, teamId, cancellationToken) is null)
        {
            return Withdrawals.NotOnTheLadder();
        }

        var now = clock.GetUtcNow();
        var request = await db.WithdrawalRequests.SingleOrDefaultAsync(r => r.TeamId == teamId && r.Status == WithdrawalStatus.Pending, cancellationToken);
        if (request is null)
        {
            request = new WithdrawalRequest { Id = Guid.NewGuid(), LeagueId = leagueId, TeamId = teamId, RequestedAt = now };
            db.WithdrawalRequests.Add(request);
        }

        request.Status = WithdrawalStatus.Accepted;
        request.DecidedAt = now;
        request.DecidedByUserId = user.UserId();
        team.WithdrawnAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
