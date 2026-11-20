using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Manager approves a Player's request to join a League. The Player becomes
/// a participant, with the request time as their join time, and with a Team of
/// one in a singles League (<see cref="LeagueTeams"/>). Approving again changes
/// nothing; approving a rejected request is a 409.
/// </summary>
public static class ApproveLeagueJoinRequest
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
            .Include(r => r.League)
            .SingleOrDefaultAsync(r => r.LeagueId == leagueId && r.PlayerId == playerId, cancellationToken);
        if (joinRequest is null)
        {
            return TypedResults.NotFound();
        }

        switch (joinRequest.Status)
        {
            case LeagueJoinRequestStatus.Approved:
                return TypedResults.NoContent();
            case LeagueJoinRequestStatus.Rejected:
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    type: "join-request-decided",
                    title: "This request was already rejected.",
                    detail: "To let the Player in after all, add them to the League directly.");
        }

        // They may have left the Club, or been removed, since asking.
        if (!await LeagueEntry.IsClubMemberAsync(db, joinRequest.League.ClubId, playerId, cancellationToken))
        {
            return LeagueEntry.NotAClubMember();
        }

        var now = clock.GetUtcNow();
        joinRequest.Status = LeagueJoinRequestStatus.Approved;
        joinRequest.DecidedAt = now;
        if (!await LeagueEntry.IsParticipantAsync(db, leagueId, playerId, cancellationToken))
        {
            LeagueTeams.AddParticipant(db, joinRequest.League, playerId, joinRequest.RequestedAt, now);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
