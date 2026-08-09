namespace SocialTennis.Api.Authentication;

/// <summary>
/// Delivery seam for magic links. v1 registers only the logging implementation
/// below; real email delivery is ticket #19, which swaps this implementation
/// without touching call sites. Integration tests substitute a capturing fake.
/// </summary>
public interface IMagicLinkSender
{
    Task SendAsync(string email, string link, CancellationToken cancellationToken);
}

/// <summary>
/// Dev-mode delivery: writes the link to the application log, satisfying #3's
/// "retrievable in local dev without a real mail provider".
/// </summary>
public class LoggingMagicLinkSender(ILogger<LoggingMagicLinkSender> logger) : IMagicLinkSender
{
    public Task SendAsync(string email, string link, CancellationToken cancellationToken)
    {
        logger.LogInformation("Magic link for {Email}: {Link}", email, link);
        return Task.CompletedTask;
    }
}
