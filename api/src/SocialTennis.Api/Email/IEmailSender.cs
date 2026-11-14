namespace SocialTennis.Api.Email;

/// <summary>One email to one recipient, with both bodies a mail client may pick from.</summary>
public record OutgoingEmail(string To, string Subject, string PlainText, string Html);

/// <summary>
/// The single seam for sending email (#19). Call sites compose an
/// <see cref="OutgoingEmail"/> and never learn which provider carries it; the
/// provider is chosen by the "Email" configuration section alone.
/// </summary>
public interface IEmailSender
{
    /// <exception cref="EmailDeliveryException">The provider did not accept the email.</exception>
    Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken);
}

/// <summary>
/// The provider refused or could not be reached. Thrown by every provider in
/// place of its own exception types, so a call site can treat "the email
/// didn't go" as an expected outcome without knowing the provider.
/// </summary>
public class EmailDeliveryException(string message, Exception innerException)
    : Exception(message, innerException);
