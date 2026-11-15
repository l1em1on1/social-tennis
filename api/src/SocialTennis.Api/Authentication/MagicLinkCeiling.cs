using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace SocialTennis.Api.Authentication;

/// <summary>
/// The ceiling on sign-in link requests across every client
/// (Auth:MagicLinkRequestsPerMinute), a backstop behind the per-client limit
/// (#108). It is checked in the handler, after the rate-limiting middleware
/// has applied the per-client limit, so a client refused there never uses up
/// the ceiling that every other client shares.
/// </summary>
public sealed class MagicLinkCeiling(IOptions<AuthOptions> options) : IDisposable
{
    private readonly FixedWindowRateLimiter _limiter = new(new FixedWindowRateLimiterOptions
    {
        PermitLimit = options.Value.MagicLinkRequestsPerMinute,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        AutoReplenishment = true,
    });

    /// <summary>Takes one request's place under the ceiling, or says it is reached.</summary>
    public bool TryEnter()
    {
        using var lease = _limiter.AttemptAcquire();
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}
