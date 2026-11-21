using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// The rules for getting into a League, shared by asking, approving, rejecting,
/// taking a request back and a Manager's add.
/// </summary>
internal static class LeagueEntry
{
    /// <summary>Advisory-lock class for League entry, apart from ClubAdmission's.</summary>
    private const int LockClass = 1_160_002;

    /// <summary>
    /// Opens a transaction holding an advisory lock on this League, released at
    /// commit or rollback. Every write to a League's requests and participants
    /// takes it first, so an approval and a Manager's add of the same Player
    /// can't both insert the participant, and a second press can't race the
    /// first into a duplicate-key error.
    /// </summary>
    public static async Task<IDbContextTransaction> LockLeagueAsync(
        TennisDbContext db, Guid leagueId, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock({LockClass}, hashtext({leagueId.ToString()}))", cancellationToken);
        return transaction;
    }

    /// <summary>Only a member of the League's Club can be in it: a pending membership doesn't count.</summary>
    public static Task<bool> IsClubMemberAsync(
        TennisDbContext db, Guid clubId, Guid playerId, CancellationToken cancellationToken) =>
        db.ClubMemberships.AnyAsync(
            m => m.ClubId == clubId && m.PlayerId == playerId && m.Status == ClubMembershipStatus.Active,
            cancellationToken);

    public static Task<bool> IsParticipantAsync(
        TennisDbContext db, Guid leagueId, Guid playerId, CancellationToken cancellationToken) =>
        db.LeagueParticipants.AnyAsync(p => p.LeagueId == leagueId && p.PlayerId == playerId, cancellationToken);

    /// <summary>
    /// Whether a Player is in a request still waiting for a Manager, as the one
    /// asking or as the partner named (#6). A Player is in at most one: asking,
    /// and being named, are refused while they are in another.
    /// </summary>
    public static Task<bool> IsInWaitingRequestAsync(
        TennisDbContext db, Guid leagueId, Guid playerId, CancellationToken cancellationToken) =>
        db.LeagueJoinRequests.AnyAsync(
            r => r.LeagueId == leagueId
                && r.Status == LeagueJoinRequestStatus.Pending
                && (r.PlayerId == playerId || r.PartnerId == playerId),
            cancellationToken);

    /// <summary>
    /// Approves a waiting request: the Player joins, with the request time as
    /// their join time; with a partner named, both join as a Team (#6).
    /// Approving and a Manager's add of the Player who asked both come here.
    /// Null when done; otherwise the 409 to return, with nothing changed.
    /// </summary>
    /// <remarks>
    /// Both must still be Club members: either may have left since asking. A
    /// partner the Manager has already added, unpaired, is paired; one already
    /// in a Team is a 409.
    /// </remarks>
    public static async Task<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult?> ApproveAsync(
        TennisDbContext db, League league, LeagueJoinRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!await IsClubMemberAsync(db, league.ClubId, request.PlayerId, cancellationToken))
        {
            return NotAClubMember();
        }

        if (request.PartnerId is { } partnerId)
        {
            if (!await IsClubMemberAsync(db, league.ClubId, partnerId, cancellationToken))
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    type: "partner-not-a-club-member",
                    title: "The partner this Player named isn't a member of the League's Club.",
                    detail: "They may have left the Club since. The Player who asked can take the request back and ask with someone else. Rejecting it stops them asking again, though a Manager can still add them.");
            }

            if (await LeagueTeams.EnterAsPairAsync(
                db, league, request.PlayerId, partnerId, request.RequestedAt, now, cancellationToken) is { } problem)
            {
                return problem;
            }
        }
        else if (!await IsParticipantAsync(db, league.Id, request.PlayerId, cancellationToken))
        {
            LeagueTeams.AddParticipant(db, league, request.PlayerId, request.RequestedAt, now);
        }

        request.Status = LeagueJoinRequestStatus.Approved;
        request.DecidedAt = now;
        return null;
    }

    public static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult NotAClubMember() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "not-a-club-member",
            title: "This Player isn't a member of the League's Club.",
            detail: "Only members of the Club can be in its Leagues. Approve their Club membership first.");
}
