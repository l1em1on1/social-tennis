using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Auth;
using SocialTennis.Api.Features.Games;
using SocialTennis.Api.Features.Leagues;
using SocialTennis.Api.Membership;
using SocialTennis.Api.Roles;

namespace SocialTennis.Api.Accounts;

/// <summary>
/// Deleting a User (#116, ADR-0024): an Admin does it for someone who asked to
/// be forgotten, and the 24-month retention job (#117) uses it too. Its own
/// top-level folder, since a job calls it as well as an endpoint
/// (docs/agents/api-endpoints.md).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Their Players become Former players:</b> named "Former player", Gender and Level cleared,
/// held by nobody. Places on ladders, Teams, Games, Scores and their history stay, so nobody else's
/// results change.</item>
/// <item><b>Memberships and requests go:</b> the Players leave their Clubs, and their League join
/// requests go, with waiting requests that name one of them as partner.</item>
/// <item><b>The User leaves no trace:</b> Scores, changes and not-played marks they made keep no
/// pointer to them (a Manager's override then reads "A manager"). Their sessions, sign-in links and
/// the invites for their email go, then the User. The email can sign up again as a new, empty
/// account.</item>
/// </list>
/// Locks, in this order, so it can't deadlock with anything taking a subset of them: roles (the
/// last-Admin check), the email's sign-in links, its admission, then for each League it touches,
/// by id, the entry lock and the results lock.
/// </remarks>
public static class AccountDeletion
{
    public enum Outcome
    {
        Deleted,
        NotFound,

        /// <summary>An Admin can't delete themselves: another Admin can.</summary>
        OwnAccount,

        /// <summary>Their email is an Admin in the app's configuration, which would let them straight back.</summary>
        AdminBySetting,

        /// <summary>They are the only Admin, and no address is configured to fall back on.</summary>
        LastAdmin,

        /// <summary>For the retention job: they have signed in since it chose them, so they stay.</summary>
        SignedInSince,
    }

    /// <param name="byUserId">The Admin deleting, refused for their own account; none for a job.</param>
    /// <param name="lastSignedInBefore">
    /// For the retention job (#117): delete only if they last signed in before this, checked under the
    /// email's sign-in lock, so someone signing in as the job runs stays.
    /// </param>
    public static async Task<Outcome> DeleteAsync(
        TennisDbContext db,
        AuthOptions auth,
        Guid userId,
        Guid? byUserId,
        CancellationToken cancellationToken,
        DateTimeOffset? lastSignedInBefore = null)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await AdminRoleRules.TakeRolesLockAsync(db, cancellationToken);

        var user = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.Email, u.Roles })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return Outcome.NotFound;
        }

        if (userId == byUserId)
        {
            return Outcome.OwnAccount;
        }

        if (auth.IsAdminEmail(user.Email))
        {
            return Outcome.AdminBySetting;
        }

        if (user.Roles.HasFlag(UserRoles.Admin))
        {
            var otherAdmins = await db.Users
                .CountAsync(u => u.Id != userId && (u.Roles & UserRoles.Admin) != 0, cancellationToken);
            if (AdminRoleRules.CanRemoveAdmin(false, auth.HasAdminEmails, otherAdmins) == AdminRoleRules.Refusal.LastAdmin)
            {
                return Outcome.LastAdmin;
            }
        }

        await AddressLock.TakeAsync(db, user.Email, cancellationToken);
        if (lastSignedInBefore is { } before
            && !await db.Users.AnyAsync(u => u.Id == userId && u.LastSignedInAt != null && u.LastSignedInAt < before, cancellationToken))
        {
            return Outcome.SignedInSince;
        }

        await ClubAdmission.TakeEmailLockAsync(db, user.Email, cancellationToken);

        var players = await db.Players.Where(p => p.UserId == userId).Select(p => p.Id).ToListAsync(cancellationToken);
        var leagues = await db.Leagues
            .Where(l => l.Participants.Any(p => players.Contains(p.PlayerId))
                || db.LeagueJoinRequests.Any(r => r.LeagueId == l.Id
                    && (players.Contains(r.PlayerId) || (r.PartnerId != null && players.Contains(r.PartnerId.Value))))
                || db.Scores.Any(s => s.LeagueId == l.Id && s.SubmittedByUserId == userId)
                || db.ScoreChanges.Any(c => c.LeagueId == l.Id && c.ByUserId == userId)
                || db.Games.Any(g => g.LeagueId == l.Id && g.VoidedByUserId == userId))
            .Select(l => l.Id)
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);
        foreach (var leagueId in leagues)
        {
            await LeagueEntry.TakeLeagueLockAsync(db, leagueId, cancellationToken);
            await GameScores.TakeResultsLockAsync(db, leagueId, cancellationToken);
        }

        await db.Players
            .Where(p => p.UserId == userId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(p => p.Name, Player.FormerPlayerName)
                    .SetProperty(p => p.Gender, (PlayerGender?)null)
                    .SetProperty(p => p.Level, (int?)null)
                    .SetProperty(p => p.UserId, (Guid?)null),
                cancellationToken);
        await db.ClubMemberships.Where(m => players.Contains(m.PlayerId)).ExecuteDeleteAsync(cancellationToken);
        // The days and usual week they said they're free (#9, #276) are theirs alone: nobody's results depend on them.
        await db.AvailableDays.Where(d => players.Contains(d.PlayerId)).ExecuteDeleteAsync(cancellationToken);
        await db.UsualDays.Where(d => players.Contains(d.PlayerId)).ExecuteDeleteAsync(cancellationToken);
        await db.LeagueJoinRequests
            .Where(r => players.Contains(r.PlayerId)
                || (r.Status == LeagueJoinRequestStatus.Pending && r.PartnerId != null && players.Contains(r.PartnerId.Value)))
            .ExecuteDeleteAsync(cancellationToken);

        await db.Scores
            .Where(s => s.SubmittedByUserId == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.SubmittedByUserId, (Guid?)null), cancellationToken);
        await db.ScoreChanges
            .Where(c => c.ByUserId == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.ByUserId, (Guid?)null), cancellationToken);
        await db.Games
            .Where(g => g.VoidedByUserId == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(g => g.VoidedByUserId, (Guid?)null), cancellationToken);

        await db.MagicLinkTokens.Where(t => t.Email == user.Email).ExecuteDeleteAsync(cancellationToken);
        await db.ClubInvites.Where(i => i.Email == user.Email).ExecuteDeleteAsync(cancellationToken);
        // Sessions, external logins and roles in Clubs go with the User (cascade).
        await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Outcome.Deleted;
    }
}
