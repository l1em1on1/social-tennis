using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SocialTennis.Api.Accounts;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;

namespace SocialTennis.Api.Jobs;

/// <summary>
/// Deletes the accounts nobody has signed in to for 24 months (#117, ADR-0024),
/// through the same path as an Admin's delete (<see cref="AccountDeletion"/>):
/// their Players become Former players, and every result stays.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Someone who has never signed in stays</b> (owner, 2026-11-26): a Manager can add a member
/// and record their scores, so a regular player may never sign in.</item>
/// <item><b>So does anyone with a Player in a League that hasn't ended</b>: one not started yet, or
/// whose latest Stage is open. They are playing, whether or not they sign in.</item>
/// <item><b>An Admin is never deleted by it</b> when that would leave nobody able to act: the deletion
/// path refuses, as it does for an Admin.</item>
/// </list>
/// Idempotent: a deleted account is gone, and one kept is kept again. The time comes from the
/// injected clock (ADR-0016). Someone who signs in while it runs stays: the deletion checks the
/// last sign-in again under the email's sign-in lock.
/// </remarks>
public sealed class PurgeInactiveAccountsJob(
    TennisDbContext db, IOptions<AuthOptions> auth, TimeProvider clock, ILogger<PurgeInactiveAccountsJob> logger) : IJob
{
    /// <summary>How long an account may go without a sign-in (ADR-0024).</summary>
    public const int Months = 24;

    public string Name => "purge-inactive-accounts";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var before = clock.GetUtcNow().AddMonths(-Months);

        var due = await db.Users
            .Where(u => u.LastSignedInAt != null && u.LastSignedInAt < before)
            .Where(u => !db.Players.Any(p => p.UserId == u.Id
                && db.LeagueParticipants.Any(lp => lp.PlayerId == p.Id
                    && lp.League.Stages.OrderByDescending(s => s.Number).Select(s => s.ClosedAt).FirstOrDefault() == null)))
            .OrderBy(u => u.Id)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var (deleted, kept) = (0, 0);
        foreach (var userId in due)
        {
            var outcome = await AccountDeletion.DeleteAsync(db, auth.Value, userId, null, cancellationToken, lastSignedInBefore: before);
            if (outcome == AccountDeletion.Outcome.Deleted)
            {
                deleted++;
            }
            else
            {
                kept++;
                logger.LogInformation("Kept an account with no sign-in since before {Before}: {Outcome}.", before, outcome);
            }
        }

        logger.LogInformation(
            "Deleted {Deleted} accounts with no sign-in since before {Before}; kept {Kept}.", deleted, before, kept);
    }
}
