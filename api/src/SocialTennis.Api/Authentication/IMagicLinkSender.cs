using System.Net;
using Microsoft.Extensions.Options;
using SocialTennis.Api.Email;

namespace SocialTennis.Api.Authentication;

/// <summary>
/// Delivery seam for magic links: the call site hands over an address and a
/// link and knows nothing about the transport. Email is the only transport in
/// v1 (ADR-0004); SMS would be a second implementation.
/// </summary>
public interface IMagicLinkSender
{
    /// <exception cref="EmailDeliveryException">The link could not be sent.</exception>
    Task SendAsync(string email, string link, CancellationToken cancellationToken);
}

/// <summary>
/// Writes the sign-in email and hands it to <see cref="IEmailSender"/>, which
/// is the log in dev and a mail provider in production. It links to the
/// privacy notice on the same site (#51): a Manager may have added the person
/// before they ever signed in.
/// </summary>
public class EmailMagicLinkSender(IEmailSender emailSender, IOptions<AuthOptions> authOptions) : IMagicLinkSender
{
    public Task SendAsync(string email, string link, CancellationToken cancellationToken)
    {
        var minutes = authOptions.Value.MagicLinkLifetimeMinutes;
        var htmlLink = WebUtility.HtmlEncode(link);
        var privacy = new Uri(new Uri(link), "/privacy").ToString();
        var htmlPrivacy = WebUtility.HtmlEncode(privacy);

        // The link sits on a line of its own: mail clients turn it into a
        // link, and the dev log and the tests read it from there.
        var plainText = $"""
            Open this link to sign in to Hits & Giggles:

            {link}

            It works once and expires in {minutes} minutes. If you didn't ask for it, you can ignore this email.

            How we use your data: {privacy}
            """;

        var html = $"""
            <!doctype html>
            <html lang="en">
              <body style="font-family: system-ui, -apple-system, 'Segoe UI', sans-serif; line-height: 1.5;">
                <h1 style="font-size: 20px;">Sign in to Hits &amp; Giggles</h1>
                <p>
                  <a href="{htmlLink}" style="display: inline-block; padding: 12px 20px; border-radius: 8px; background: #166534; color: #ffffff; font-weight: 600; text-decoration: none;">Sign in</a>
                </p>
                <p>Or copy this address into your browser:<br><a href="{htmlLink}">{htmlLink}</a></p>
                <p>It works once and expires in {minutes} minutes. If you didn't ask for it, you can ignore this email.</p>
                <p style="font-size: 14px; color: #555555;"><a href="{htmlPrivacy}">How we use your data</a></p>
              </body>
            </html>
            """;

        return emailSender.SendAsync(
            new OutgoingEmail(email, "Your sign-in link for Hits & Giggles", plainText, html),
            cancellationToken);
    }
}
