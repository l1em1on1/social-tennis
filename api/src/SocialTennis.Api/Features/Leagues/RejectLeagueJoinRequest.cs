using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Manager rejects a Player's request to join a League. The request is kept,
/// so the Player sees the outcome. Rejecting again changes nothing; rejecting
/// an approved request is a 409 (the Player is in the League by then).
/// </summary>
public static class RejectLeagueJoinRequest
{
    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
        Guid playerId,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        await using var transaction = await LeagueEntry.LockLeagueAsync(db, leagueId, cancellationToken);
        var joinRequest = await db.LeagueJoinRequests
            .SingleOrDefaultAsync(r => r.LeagueId == leagueId && r.PlayerId == playerId, cancellationToken);
        if (joinRequest is null)
        {
            return TypedResults.NotFound();
        }

        switch (joinRequest.Status)
        {
            case LeagueJoinRequestStatus.Rejected:
                return TypedResults.NoContent();
            case LeagueJoinRequestStatus.Approved:
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    type: "join-request-decided",
                    title: "This request was already approved.",
                    detail: "The Player is in the League.");
        }

        joinRequest.Status = LeagueJoinRequestStatus.Rejected;
        joinRequest.DecidedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
