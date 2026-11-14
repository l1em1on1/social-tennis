namespace SocialTennis.Api.Email;

/// <summary>
/// Dev-mode delivery: writes the email to the application log, so a sign-in
/// link is retrievable locally without a mail provider (#3).
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Email for {To}: {Subject}\n{Body}", email.To, email.Subject, email.PlainText);
        return Task.CompletedTask;
    }
}
