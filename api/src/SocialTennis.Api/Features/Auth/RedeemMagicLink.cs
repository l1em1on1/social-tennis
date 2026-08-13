using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Auth.Contracts;

namespace SocialTennis.Api.Features.Auth;

/// <summary>
/// Redeem is where single-use and expiry are enforced; the raw magic token dies
/// here and an opaque session credential replaces it.
/// </summary>
public static class RedeemMagicLink
{
    public static async Task<Results<Ok<RedeemMagicLinkResponse>, UnauthorizedHttpResult>> HandleAsync(
        RedeemMagicLinkRequest request,
        TennisDbContext db,
        IOptions<AuthOptions> authOptions,
        CancellationToken cancellationToken)
    {
        var hash = Tokens.Hash(request.Token);
        var now = DateTimeOffset.UtcNow;
        var magicToken = await db.MagicLinkTokens
            .SingleOrDefaultAsync(t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now, cancellationToken);

        if (magicToken is null)
        {
            return TypedResults.Unauthorized();
        }

        magicToken.UsedAt = now;

        var rawSession = Tokens.NewToken();
        var expiresAt = now.AddDays(authOptions.Value.SessionLifetimeDays);
        db.SessionTokens.Add(new SessionToken
        {
            Id = Guid.NewGuid(),
            UserId = magicToken.UserId,
            TokenHash = Tokens.Hash(rawSession),
            ExpiresAt = expiresAt,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new RedeemMagicLinkResponse(rawSession, expiresAt));
    }
}
