using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Auth.Contracts;

namespace SocialTennis.Api.Features.Auth;

/// <summary>
/// Always 202: the response must not reveal whether the address has an account
/// (no user enumeration). Unknown addresses create the User — sign-up and login
/// are the same gesture. An address that already holds its quota of live links
/// gets the same 202 and no new email.
/// </summary>
public static class RequestMagicLink
{
    public static async Task<Accepted> HandleAsync(
        MagicLinkRequest request,
        TennisDbContext db,
        IMagicLinkSender sender,
        IOptions<AuthOptions> authOptions,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
        var now = clock.GetUtcNow();

        if (user is null)
        {
            user = new User { Id = Guid.NewGuid(), Email = email, CreatedAt = now };
            db.Users.Add(user);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Two first requests for one address raced, and the other one
                // created the User. Use theirs rather than answering 500.
                db.Entry(user).State = EntityState.Detached;
                user = await db.Users.SingleAsync(u => u.Email == email, cancellationToken);
            }
        }

        var liveLinks = await db.MagicLinkTokens.CountAsync(
            t => t.UserId == user.Id && t.UsedAt == null && t.ExpiresAt > now,
            cancellationToken);
        if (liveLinks >= authOptions.Value.MaxLiveMagicLinksPerAddress)
        {
            return TypedResults.Accepted((string?)null);
        }

        var rawToken = Tokens.NewToken();
        db.MagicLinkTokens.Add(new MagicLinkToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Tokens.Hash(rawToken),
            ExpiresAt = now.AddMinutes(authOptions.Value.MagicLinkLifetimeMinutes),
            CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);

        var link = $"{authOptions.Value.VerifyUrlBase}?token={Uri.EscapeDataString(rawToken)}";
        await sender.SendAsync(email, link, cancellationToken);

        return TypedResults.Accepted((string?)null);
    }
}
