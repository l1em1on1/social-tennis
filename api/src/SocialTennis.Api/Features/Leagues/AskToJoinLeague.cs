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
/// member of, alone or, in a doubles League, naming a partner (#6). The request
/// waits for a Manager; approving it puts a pair in as a Team.
/// </summary>
/// <remarks>
/// A League at a Club the Player isn't in is a 404, as if it weren't there
/// (ADR-0017), and so is a partner who isn't a member of it. Asking twice is a
/// 409, whatever became of the first request: a rejected Player can't ask
/// again, though a Manager can still add them.
/// <para>
/// The partner doesn't agree in the app; the pair agree beforehand (owner,
/// 2026-11-20). They may be held by the same User. A Player is in at most one
/// waiting request per League, as asker or partner: one already named can't
/// ask, and can't be named again, until that request is decided or taken back.
/// A partner already in the League without a partner is fine; one in a Team
/// is a 409.
/// </para>
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
        var league = await db.Leagues
            .Where(l => l.Id == leagueId)
            .Select(l => new { l.ClubId, l.Type })
            .SingleOrDefaultAsync(cancellationToken);
        if (league is null || !await LeagueEntry.IsClubMemberAsync(db, league.ClubId, playerId, cancellationToken))
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

        if (await LeagueEntry.IsInWaitingRequestAsync(db, leagueId, playerId, cancellationToken))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "named-as-partner",
                title: "Another Player has asked to join with this Player.",
                detail: "Take that request back first to ask yourself.");
        }

        if (request.PartnerId is { } partnerId)
        {
            if (league.Type.TeamSize() != 2)
            {
                return LeagueTeams.NotDoubles();
            }

            if (!await LeagueEntry.IsClubMemberAsync(db, league.ClubId, partnerId, cancellationToken))
            {
                return TypedResults.NotFound();
            }

            if (await db.LeagueParticipants.AnyAsync(
                p => p.LeagueId == leagueId && p.PlayerId == partnerId && p.TeamId != null, cancellationToken))
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    type: "partner-in-a-team",
                    title: "That partner already has a partner in this League.");
            }

            if (await LeagueEntry.IsInWaitingRequestAsync(db, leagueId, partnerId, cancellationToken))
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    type: "partner-in-a-request",
                    title: "That partner is already in a request to join this League.",
                    detail: "They asked themselves, or someone else named them. It has to be decided or taken back first.");
            }
        }

        db.LeagueJoinRequests.Add(new LeagueJoinRequest
        {
            LeagueId = leagueId,
            PlayerId = playerId,
            PartnerId = request.PartnerId,
            Status = LeagueJoinRequestStatus.Pending,
            RequestedAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
