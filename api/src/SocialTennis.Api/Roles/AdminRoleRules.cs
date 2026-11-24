using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SocialTennis.Api.Data;

namespace SocialTennis.Api.Roles;

/// <summary>
/// When the Admin role may be taken from a User. The app must never be left
/// with nobody able to act (issue #18).
/// </summary>
public static class AdminRoleRules
{
    private const int LockClass = 1_160_003;

    public enum Refusal
    {
        /// <summary>The role can be removed.</summary>
        None,

        /// <summary>Their email is in Auth:AdminEmails, which only a deployment can change.</summary>
        AdminBySetting,

        /// <summary>They are the only Admin, and no address is configured to fall back on.</summary>
        LastAdmin,
    }

    /// <param name="isAdminBySetting">The User's email is in Auth:AdminEmails.</param>
    /// <param name="anyAdminBySetting">Auth:AdminEmails lists at least one address.</param>
    /// <param name="otherGrantedAdmins">Other Users granted the Admin role.</param>
    public static Refusal CanRemoveAdmin(bool isAdminBySetting, bool anyAdminBySetting, int otherGrantedAdmins)
    {
        if (isAdminBySetting)
        {
            return Refusal.AdminBySetting;
        }

        // A configured address can always sign in and be an Admin, so with one
        // listed the app can't become admin-less whoever is demoted.
        return anyAdminBySetting || otherGrantedAdmins > 0 ? Refusal.None : Refusal.LastAdmin;
    }

    /// <summary>
    /// Opens a transaction holding the one lock every role change takes, so two
    /// Admins demoting each other at the same moment can't both pass the
    /// last-Admin check.
    /// </summary>
    public static async Task<IDbContextTransaction> LockRolesAsync(TennisDbContext db, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await TakeRolesLockAsync(db, cancellationToken);
        return transaction;
    }

    /// <summary>Takes the roles lock in the transaction already open, as deleting a User does first (#116).</summary>
    public static async Task TakeRolesLockAsync(TennisDbContext db, CancellationToken cancellationToken) =>
        // Two-key form, its own class: apart from the per-email admission locks
        // (1160001, Membership/ClubAdmission.cs), the per-League entry locks
        // (1160002, Features/Leagues/LeagueEntry.cs) and the startup migration lock.
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({LockClass}, 0)", cancellationToken);
}
