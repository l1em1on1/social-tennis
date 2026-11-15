using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SocialTennis.Api.Data;

namespace SocialTennis.Api.Features.Auth;

/// <summary>
/// One lock per email address for its sign-in links (#108). Asking for a link
/// takes it to count and insert as one step; redeeming takes it so that two of
/// the address's links redeemed at once run one after the other. Without it
/// each would claim its own link and then wait on the other's while using up
/// the address's other links: a deadlock, which Postgres ends by failing one.
/// An advisory lock, not a row lock, because the address may have no User yet.
/// </summary>
internal static class AddressLock
{
    /// <summary>Advisory-lock class for sign-in links, so its keys can't meet another lock's.</summary>
    private const int LockClass = 1_160_004;

    /// <summary>Opens a transaction holding the lock for this address, released at commit or rollback.</summary>
    public static async Task<IDbContextTransaction> BeginAsync(
        TennisDbContext db, string normalizedEmail, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock({LockClass}, hashtext({normalizedEmail}))", cancellationToken);
        return transaction;
    }
}
