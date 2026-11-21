using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A request to join a League is taken back while it waits: by the Player who
/// asked, or by the partner it names (#6). It is deleted, so the Player who
/// asked may ask again, and the partner is free to.
/// </summary>
/// <remarks>
/// The request is addressed by the Player who asked. One the signed-in User
/// holds neither Player of is a 404, as if it weren't there (ADR-0017). A
/// decided request is a 409: an approved Player is in the League, and a
/// rejected one stays rejected.
/// </remarks>
public static class TakeBackLeagueJoinRequest
{
    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
        Guid playerId,
        ClaimsPrincipal user,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        await using var transaction = await LeagueEntry.LockLeagueAsync(db, leagueId, cancellationToken);
        var joinRequest = await db.LeagueJoinRequests
            .Where(r => r.LeagueId == leagueId
                && r.PlayerId == playerId
                && (r.Player.UserId == userId || (r.Partner != null && r.Partner.UserId == userId)))
            .SingleOrDefaultAsync(cancellationToken);
        if (joinRequest is null)
        {
            return TypedResults.NotFound();
        }

        if (joinRequest.Status != LeagueJoinRequestStatus.Pending)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "join-request-decided",
                title: "A Manager has already decided this request.");
        }

        db.LeagueJoinRequests.Remove(joinRequest);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
