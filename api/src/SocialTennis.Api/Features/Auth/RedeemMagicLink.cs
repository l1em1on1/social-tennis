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
/// here and an opaque session credential replaces it. It is also where a User
/// is created: the first link an address redeems makes its User (#108).
/// </summary>
public static class RedeemMagicLink
{
    public static async Task<Results<Ok<RedeemMagicLinkResponse>, UnauthorizedHttpResult>> HandleAsync(
        RedeemMagicLinkRequest request,
        TennisDbContext db,
        IOptions<AuthOptions> authOptions,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        // No body, or a null token, is just a link that doesn't work (#109).
        if (string.IsNullOrEmpty(request.Token))
        {
            return TypedResults.Unauthorized();
        }

        var hash = Tokens.Hash(request.Token);
        var now = clock.GetUtcNow();
        var magicToken = await db.MagicLinkTokens
            .Where(t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now)
            .Select(t => new { t.Id, t.Email })
            .SingleOrDefaultAsync(cancellationToken);

        if (magicToken is null)
        {
            return TypedResults.Unauthorized();
        }

        // One redemption at a time per address (AddressLock explains why).
        await using var transaction = await AddressLock.BeginAsync(db, magicToken.Email, cancellationToken);

        // Claimed by one conditional update, so two redemptions at the same
        // moment can't both pass the check above and mint two sessions: the
        // second waits on the row, then finds UsedAt set and updates nothing.
        var claimed = await db.MagicLinkTokens
            .Where(t => t.Id == magicToken.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.UsedAt, now), cancellationToken);
        if (claimed != 1)
        {
            return TypedResults.Unauthorized();
        }

        // The address's other unused links go with it: signing in with one
        // link leaves no other way in lying in the inbox.
        await db.MagicLinkTokens
            .Where(t => t.Email == magicToken.Email && t.UsedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.UsedAt, now), cancellationToken);

        // The address's User, created now if this is its first sign-in.
        var userId = await UserAccounts.EnsureAsync(db, magicToken.Email, now, cancellationToken);

        var rawSession = Tokens.NewToken();
        var expiresAt = now.AddDays(authOptions.Value.SessionLifetimeDays);
        db.SessionTokens.Add(new SessionToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Tokens.Hash(rawSession),
            ExpiresAt = expiresAt,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.Ok(new RedeemMagicLinkResponse(rawSession, expiresAt));
    }
}
