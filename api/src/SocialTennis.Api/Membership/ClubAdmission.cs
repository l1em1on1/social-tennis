using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Membership;

/// <summary>
/// Who is in a Club without asking (#164). An invite lets in the Players its
/// email's User holds, and is used up as it does, so invites exist only while
/// that User holds no Player. A User is in a Club once they hold an approved
/// member of it; from then on every Player of theirs, added or asking, is a
/// member at once. A request still waiting doesn't count. Applied when
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
    /// Lets the Players this email's User holds into a Club: each one not in it
    /// joins, and each one whose request was waiting is approved. Pre-approving
    /// an email and a Manager adding a Player both do this (#224). Call it holding
    /// <see cref="LockEmailAsync"/>, before saving. Returns how many Players the
    /// User holds, none if the email has no User yet.
    /// </summary>
    public static async Task<int> LetInAsync(
        TennisDbContext db, string normalizedEmail, Guid clubId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var players = await db.Players
            .Where(p => p.User.Email == normalizedEmail)
            .Select(p => new { p.Id, IsMember = p.Memberships.Any(m => m.ClubId == clubId) })
            .ToListAsync(cancellationToken);
        foreach (var player in players.Where(p => !p.IsMember))
        {
            db.ClubMemberships.Add(Membership(clubId, player.Id, ClubMembershipStatus.Active, now));
        }

        // Waiting requests become memberships in one conditional update, so a
        // Player leaving at this moment (which takes no lock) is skipped
        // rather than failing (#109).
        var waiting = players.Where(p => p.IsMember).Select(p => p.Id).ToList();
        await db.ClubMemberships
            .Where(m => m.ClubId == clubId && waiting.Contains(m.PlayerId))
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.Status, ClubMembershipStatus.Active), cancellationToken);
        return players.Count;
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

        // An invite lets the User in, so their requests waiting in those Clubs go too.
        foreach (var clubId in invited.Except(memberOf))
        {
            await ApproveWaitingAsync(db, userId, clubId, cancellationToken);
        }

        await UseUpInvitesAsync(db, normalizedEmail, cancellationToken);
    }

    /// <summary>
    /// The User is in this Club now, so their requests still waiting there are
    /// approved: every path that lets a User in calls this (#164). A conditional
    /// update, so a request taken back at the same moment is skipped.
    /// </summary>
    public static Task<int> ApproveWaitingAsync(
        TennisDbContext db, Guid userId, Guid clubId, CancellationToken cancellationToken) =>
        db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.Status == ClubMembershipStatus.Pending && m.Player.UserId == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.Status, ClubMembershipStatus.Active), cancellationToken);

    /// <summary>Deletes this email's invites: of one Club, or of every Club when <paramref name="clubId"/> is null.</summary>
    public static Task<int> UseUpInvitesAsync(
        TennisDbContext db, string normalizedEmail, CancellationToken cancellationToken, Guid? clubId = null) =>
        db.ClubInvites
            .Where(i => i.Email == normalizedEmail && (clubId == null || i.ClubId == clubId))
            .ExecuteDeleteAsync(cancellationToken);

    public static ClubMembership Membership(Guid clubId, Guid playerId, ClubMembershipStatus status, DateTimeOffset now) =>
        new() { ClubId = clubId, PlayerId = playerId, Status = status, RequestedAt = now };
}
