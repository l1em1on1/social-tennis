using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Auth;

/// <summary>
/// Always 202: the response must not reveal whether the address has an account
/// (no user enumeration). Unknown addresses create the User — sign-up and login
/// are the same gesture.
/// </summary>
public static class RequestMagicLink
{
    public static async Task<Accepted> HandleAsync(
        MagicLinkRequest request,
        TennisDbContext db,
        IMagicLinkSender sender,
        IOptions<AuthOptions> authOptions,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            user = new User { Id = Guid.NewGuid(), Email = email, CreatedAt = DateTimeOffset.UtcNow };
            db.Users.Add(user);
        }

        var rawToken = Tokens.NewToken();
        db.MagicLinkTokens.Add(new MagicLinkToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Tokens.Hash(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(authOptions.Value.MagicLinkLifetimeMinutes),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);

        var link = $"{authOptions.Value.VerifyUrlBase}?token={Uri.EscapeDataString(rawToken)}";
        await sender.SendAsync(email, link, cancellationToken);

        return TypedResults.Accepted((string?)null);
    }
}
