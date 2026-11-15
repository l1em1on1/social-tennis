using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// One of the signed-in User's Players asks to join a League at a Club it is a
/// member of. The request waits for a Manager. Solo only: joining with a
/// partner is #6.
/// </summary>
/// <remarks>
/// A League at a Club the Player isn't in is a 404, as if it weren't there
/// (ADR-0017). Asking twice is a 409, whatever became of the first request:
/// a rejected Player can't ask again, though a Manager can still add them.
/// </remarks>
public static class AskToJoinLeague
{
    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
        AskToJoinLeagueRequest request,
        ClaimsPrincipal user,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        var playerId = request.PlayerId;
        if (!await db.Players.AnyAsync(p => p.Id == playerId && p.UserId == userId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        await using var transaction = await LeagueEntry.LockLeagueAsync(db, leagueId, cancellationToken);
        var clubId = await db.Leagues
            .Where(l => l.Id == leagueId)
            .Select(l => (Guid?)l.ClubId)
            .SingleOrDefaultAsync(cancellationToken);
        if (clubId is null || !await LeagueEntry.IsClubMemberAsync(db, clubId.Value, playerId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        if (await LeagueEntry.IsParticipantAsync(db, leagueId, playerId, cancellationToken))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "already-in-league",
                title: "This Player is already in the League.");
        }

        if (await db.LeagueJoinRequests.AnyAsync(r => r.LeagueId == leagueId && r.PlayerId == playerId, cancellationToken))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "already-asked",
                title: "This Player has already asked to join the League.",
                detail: "A Player asks once. A Manager can still add them directly.");
        }

        db.LeagueJoinRequests.Add(new LeagueJoinRequest
        {
            LeagueId = leagueId,
            PlayerId = playerId,
            Status = LeagueJoinRequestStatus.Pending,
            RequestedAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
