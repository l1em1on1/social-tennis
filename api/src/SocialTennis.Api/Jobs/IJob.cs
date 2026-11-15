namespace SocialTennis.Api.Jobs;

/// <summary>
/// A scheduled job: run once, then exit (ADR-0019, #112). The PoC is hosted
/// scaled to zero, so nothing runs in the background; a Container Apps Job
/// starts the API image with <c>job &lt;name&gt;</c> on a cron schedule, and
/// compose runs the same command by hand.
/// </summary>
/// <remarks>
/// A job must be idempotent: running it twice, or late, gives the same result.
/// Time comes from the injected <see cref="TimeProvider"/> (ADR-0016).
/// </remarks>
public interface IJob
{
    /// <summary>The name on the command line: <c>job purge-tokens</c>.</summary>
    string Name { get; }

    Task RunAsync(CancellationToken cancellationToken);
}
