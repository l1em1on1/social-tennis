using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// The rules for getting into a League, shared by asking, approving, rejecting
/// and a Manager's add.
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

    public static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult NotAClubMember() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "not-a-club-member",
            title: "This Player isn't a member of the League's Club.",
            detail: "Only members of the Club can be in its Leagues. Approve their Club membership first.");
}
