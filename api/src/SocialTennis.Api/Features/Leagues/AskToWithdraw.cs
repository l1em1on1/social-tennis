using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Player asks to withdraw their Team from a running League (#21). Nothing
/// changes until the Manager accepts; the Player can cancel it while it waits.
/// One waits per Team: in doubles either partner may ask, not both.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A Player the signed-in User doesn't hold, or one not in the League: 404.</item>
/// <item>Not on the running ladder (not started, ended, or left off): 409 <c>not-on-the-ladder</c>; the Manager removes them instead.</item>
/// <item>Already withdrawn: 409 <c>already-withdrawn</c>. A request already waiting for the Team: 409 <c>already-asked</c>.</item>
/// </list>
/// A Player asked to leave before and was declined may ask again.
/// </remarks>
public static class AskToWithdraw
{
    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
        AskToWithdrawRequest request,
        ClaimsPrincipal user,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        await using var transaction = await LeagueEntry.LockLeagueAsync(db, leagueId, cancellationToken);
        var team = await db.LeagueParticipants
            .Where(p => p.LeagueId == leagueId && p.PlayerId == request.PlayerId && p.Player.UserId == userId && p.TeamId != null)
            .Select(p => new { Id = p.TeamId!.Value, Withdrawn = p.Team!.WithdrawnAt != null })
            .SingleOrDefaultAsync(cancellationToken);
        if (team is null)
        {
            return TypedResults.NotFound();
        }

        if (team.Withdrawn)
        {
            return Withdrawals.AlreadyWithdrawn();
        }

        if (await Withdrawals.OpenStageWithAsync(db, leagueId, team.Id, cancellationToken) is null)
        {
            return Withdrawals.NotOnTheLadder();
        }

        if (await db.WithdrawalRequests.AnyAsync(r => r.TeamId == team.Id && r.Status == WithdrawalStatus.Pending, cancellationToken))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "already-asked",
                title: "A request to withdraw this Team is already waiting on the Manager.");
        }

        db.WithdrawalRequests.Add(new WithdrawalRequest
        {
            Id = Guid.NewGuid(),
            LeagueId = leagueId,
            TeamId = team.Id,
            PlayerId = request.PlayerId,
            RequestedAt = clock.GetUtcNow(),
            Status = WithdrawalStatus.Pending,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
