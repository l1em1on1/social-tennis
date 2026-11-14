using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Membership;

/// <summary>
/// The one rule for who is in a Club without asking: Players held by a User
/// whose email a Manager has invited. Applied in four places — inviting,
/// asking to join, adding a Player, removing a member — across two features,
/// so it lives here rather than in either.
/// </summary>
internal static class ClubAdmission
{
    /// <summary>Advisory-lock class for admission, so its keys can't meet another lock's.</summary>
    private const int LockClass = 1_160_001;

    /// <summary>
    /// Opens a transaction holding an advisory lock on this email, released at
    /// commit or rollback. Every write that decides admission for an email
    /// takes it first, so their read-then-write steps can't interleave:
    /// without it, an invite made while that User adds a Player can miss the
    /// new Player, because neither sees the other's uncommitted row.
    /// </summary>
    /// <remarks>
    /// The two-key form of the lock: Postgres keeps it apart from the
    /// single-key space the startup migration lock uses (Program.cs).
    /// </remarks>
    public static async Task<IDbContextTransaction> LockEmailAsync(
        TennisDbContext db, string normalizedEmail, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock({LockClass}, hashtext({normalizedEmail}))", cancellationToken);
        return transaction;
    }

    /// <summary>Ids of the Clubs that have invited this email.</summary>
    public static Task<List<Guid>> InvitingClubIdsAsync(
        TennisDbContext db, string normalizedEmail, CancellationToken cancellationToken) =>
        db.ClubInvites
            .Where(i => i.Email == normalizedEmail)
            .Select(i => i.ClubId)
            .ToListAsync(cancellationToken);

    public static ClubMembership Membership(Guid clubId, Guid playerId, ClubMembershipStatus status, DateTimeOffset now) =>
        new() { ClubId = clubId, PlayerId = playerId, Status = status, RequestedAt = now };
}
