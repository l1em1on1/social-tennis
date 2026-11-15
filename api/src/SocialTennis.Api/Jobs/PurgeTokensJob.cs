using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;

namespace SocialTennis.Api.Jobs;

/// <summary>
/// Deletes sign-in links and sessions that can never be used again: links used
/// or expired, sessions expired or revoked (#19, #112). Nothing else reads them;
/// a live link or session is never touched.
/// </summary>
public sealed class PurgeTokensJob(TennisDbContext db, TimeProvider clock, ILogger<PurgeTokensJob> logger) : IJob
{
    public string Name => "purge-tokens";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        var links = await db.MagicLinkTokens
            .Where(t => t.UsedAt != null || t.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
        var sessions = await db.SessionTokens
            .Where(s => s.RevokedAt != null || s.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation(
            "Purged {Links} spent sign-in links and {Sessions} ended sessions.", links, sessions);
    }
}
