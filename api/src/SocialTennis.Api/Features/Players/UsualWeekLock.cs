using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SocialTennis.Api.Data;

namespace SocialTennis.Api.Features.Players;

/// <summary>
/// One lock per Player for saving their usual week (#276). A save deletes the
/// week and inserts the new one: two at once would both insert, and one would
/// fail on the key. With the lock they take turns, and the last one stands.
/// </summary>
internal static class UsualWeekLock
{
    /// <summary>Advisory-lock class for usual weeks, so its keys can't meet another lock's.</summary>
    private const int LockClass = 1_160_006;

    /// <summary>Opens a transaction holding the lock for this Player, released at commit or rollback.</summary>
    public static async Task<IDbContextTransaction> BeginAsync(TennisDbContext db, Guid playerId, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock({LockClass}, hashtext({playerId.ToString()}))", cancellationToken);
        return transaction;
    }
}
