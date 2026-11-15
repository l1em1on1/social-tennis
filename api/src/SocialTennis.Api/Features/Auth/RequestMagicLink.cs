using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Email;
using SocialTennis.Api.Features.Auth.Contracts;

namespace SocialTennis.Api.Features.Auth;

/// <summary>
/// Always 202: the response must not reveal whether the address has an account
/// (no user enumeration). Asking for a link creates nothing but the link: the
/// User is created when a link is redeemed (#108), so sign-up and login stay
/// the same gesture without an unanswered request leaving a User behind. An
/// address that already holds its quota of live links gets the same 202 and
/// no new email.
/// <para>
/// 429 when this client has asked too often (the rate-limiting middleware,
/// per client) or every client together has (<see cref="MagicLinkCeiling"/>).
/// </para>
/// <para>
/// 503 when the email could not be handed to the mail provider. That says
/// nothing about the address either: every address is sent to. The link that
/// reached nobody is deleted, so retrying doesn't use up the address's quota.
/// </para>
/// </summary>
public static class RequestMagicLink
{
    public static async Task<Results<Accepted, ProblemHttpResult>> HandleAsync(
        MagicLinkRequest request,
        HttpContext http,
        TennisDbContext db,
        IMagicLinkSender sender,
        MagicLinkCeiling ceiling,
        IOptions<AuthOptions> authOptions,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!ceiling.TryEnter())
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too many sign-in requests.",
                detail: "Try again in a minute.");
        }

        var email = EmailAddress.Normalize(request.Email);
        var clientHash = Tokens.Hash(ClientAddress.KeyOf(http));
        var options = authOptions.Value;
        var now = clock.GetUtcNow();

        // Count and insert as one step per address. Without the lock, two
        // requests could both count one short of the quota and both send.
        // Requests for the same address queue here; others are unaffected.
        await using var transaction = await AddressLock.BeginAsync(db, email, cancellationToken);

        var live = db.MagicLinkTokens.Where(t => t.Email == email && t.UsedAt == null && t.ExpiresAt > now);
        var liveFromEveryClient = await live.CountAsync(cancellationToken);
        var liveFromThisClient = await live.CountAsync(t => t.ClientHash == clientHash, cancellationToken);
        if (liveFromEveryClient >= options.MaxLiveMagicLinksPerAddress
            || liveFromThisClient >= options.MaxLiveMagicLinksPerAddressAndClient)
        {
            return TypedResults.Accepted((string?)null);
        }

        var rawToken = Tokens.NewToken();
        var tokenId = Guid.NewGuid();
        db.MagicLinkTokens.Add(new MagicLinkToken
        {
            Id = tokenId,
            Email = email,
            ClientHash = clientHash,
            TokenHash = Tokens.Hash(rawToken),
            ExpiresAt = now.AddMinutes(options.MagicLinkLifetimeMinutes),
            CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var link = $"{options.VerifyUrlBase}?token={Uri.EscapeDataString(rawToken)}";
        try
        {
            await sender.SendAsync(email, link, cancellationToken);
        }
        catch (Exception exception)
        {
            // However the send ended (refused, cancelled because the caller
            // went away, or a bug), the link reached nobody, so it must not
            // count against the address's quota. Not the request's
            // cancellation token: the caller may be the one who has gone.
            await db.MagicLinkTokens.Where(t => t.Id == tokenId).ExecuteDeleteAsync(CancellationToken.None);
            if (exception is not EmailDeliveryException)
            {
                throw;
            }

            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The sign-in email could not be sent.",
                detail: "Nothing was sent. Try again in a moment.");
        }

        return TypedResults.Accepted((string?)null);
    }
}
