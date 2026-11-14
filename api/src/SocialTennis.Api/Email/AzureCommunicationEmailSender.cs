using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;

namespace SocialTennis.Api.Email;

/// <summary>Sends through Azure Communication Services Email (ADR-0020).</summary>
public class AzureCommunicationEmailSender(
    EmailClient client,
    IOptions<EmailOptions> options,
    ILogger<AzureCommunicationEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        var message = new EmailMessage(
            senderAddress: options.Value.FromAddress,
            recipientAddress: email.To,
            content: new EmailContent(email.Subject) { PlainText = email.PlainText, Html = email.Html });

        try
        {
            // Started, not Completed: return once the service has accepted the
            // email rather than polling until it has left, which takes seconds
            // the person at the login form would spend waiting. What can go
            // wrong on our side (credentials, an unverified domain, the sender
            // address, throttling) is refused at this point.
            var operation = await client.SendAsync(WaitUntil.Started, message, cancellationToken);
            logger.LogInformation("Email accepted by Azure Communication Services, operation {OperationId}", operation.Id);
        }
        // Everything but the caller giving up. A refusal is a
        // RequestFailedException; a service that can't be reached surfaces,
        // once the retries are spent, as an AggregateException, and a timeout
        // as a cancellation the caller never asked for.
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                exception,
                "Azure Communication Services did not accept the email: {ErrorCode}",
                (exception as RequestFailedException)?.ErrorCode ?? exception.GetType().Name);
            throw new EmailDeliveryException("Azure Communication Services did not accept the email.", exception);
        }
    }
}
