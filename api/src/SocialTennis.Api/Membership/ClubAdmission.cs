using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Membership;

/// <summary>
/// Who is in a Club without asking (#164). An invite lets in the Players its
/// email's User holds, and is used up as it does, so invites exist only while
/// that User holds no Player. After that, every Player the User adds is a
/// member of each Club they are already in. Applied when
/// inviting, adding a Player (the User or a Manager), asking to join and
/// removing a member, across two features, so it lives here.
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

    /// <summary>
    /// Makes a Player the User has just been given a member of every Club that
    /// invited the User's email, and of every Club the User already holds a
    /// member of, and uses those invites up. Call it holding
    /// <see cref="LockEmailAsync"/>, before saving. <paramref name="except"/>
    /// is a Club the caller has already added the Player to.
    /// </summary>
    public static async Task AdmitNewPlayerAsync(
        TennisDbContext db,
        Guid userId,
        string normalizedEmail,
        Guid playerId,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        Guid? except = null)
    {
        var invited = await db.ClubInvites
            .Where(i => i.Email == normalizedEmail)
            .Select(i => i.ClubId)
            .ToListAsync(cancellationToken);
        var memberOf = await db.ClubMemberships
            .Where(m => m.Player.UserId == userId && m.Status == ClubMembershipStatus.Active)
            .Select(m => m.ClubId)
            .ToListAsync(cancellationToken);

        foreach (var clubId in invited.Concat(memberOf).Distinct().Where(id => id != except))
        {
            db.ClubMemberships.Add(Membership(clubId, playerId, ClubMembershipStatus.Active, now));
        }

        await UseUpInvitesAsync(db, normalizedEmail, cancellationToken);
    }

    /// <summary>Deletes this email's invites: of one Club, or of every Club when <paramref name="clubId"/> is null.</summary>
    public static Task<int> UseUpInvitesAsync(
        TennisDbContext db, string normalizedEmail, CancellationToken cancellationToken, Guid? clubId = null) =>
        db.ClubInvites
            .Where(i => i.Email == normalizedEmail && (clubId == null || i.ClubId == clubId))
            .ExecuteDeleteAsync(cancellationToken);

    public static ClubMembership Membership(Guid clubId, Guid playerId, ClubMembershipStatus status, DateTimeOffset now) =>
        new() { ClubId = clubId, PlayerId = playerId, Status = status, RequestedAt = now };
}
