using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Auth;

public record MagicLinkRequest(string Email);

public record RedeemRequest(string Token);

public record SessionResponse(string Token, DateTimeOffset ExpiresAt);

public record MeResponse(Guid UserId, string Email);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        // Always 202: the response must not reveal whether the address has an
        // account (no user enumeration). Unknown addresses create the User —
        // sign-up and login are the same gesture.
        group.MapPost("/magic-link", async Task<Results<Accepted, ValidationProblem>> (
            MagicLinkRequest request,
            TennisDbContext db,
            IMagicLinkSender sender,
            IOptions<AuthOptions> authOptions,
            CancellationToken cancellationToken) =>
        {
            var email = request.Email.Trim().ToLowerInvariant();
            if (email.Length is 0 or > 320 || !email.Contains('@'))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["email"] = ["A valid email address is required."],
                });
            }

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
        }).WithName("RequestMagicLink");

        // Redeem is where single-use and expiry are enforced; the raw magic
        // token dies here and an opaque session credential replaces it.
        group.MapPost("/sessions", async Task<Results<Ok<SessionResponse>, UnauthorizedHttpResult>> (
            RedeemRequest request,
            TennisDbContext db,
            IOptions<AuthOptions> authOptions,
            CancellationToken cancellationToken) =>
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

            return TypedResults.Ok(new SessionResponse(rawSession, expiresAt));
        }).WithName("RedeemMagicLink");

        group.MapGet("/me", Results<Ok<MeResponse>, UnauthorizedHttpResult> (ClaimsPrincipal user) =>
        {
            var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = user.FindFirstValue(ClaimTypes.Email);
            return id is null || email is null
                ? TypedResults.Unauthorized()
                : TypedResults.Ok(new MeResponse(Guid.Parse(id), email));
        }).RequireAuthorization().WithName("GetCurrentUser");

        // Logout revokes the presented session server-side — the credential is
        // dead even if a copy of the cookie survives somewhere.
        group.MapDelete("/sessions/current", async Task<Results<NoContent, UnauthorizedHttpResult>> (
            HttpContext context,
            TennisDbContext db,
            CancellationToken cancellationToken) =>
        {
            string? header = context.Request.Headers.Authorization;
            if (header is null || !header.StartsWith("Bearer ", StringComparison.Ordinal))
            {
                return TypedResults.Unauthorized();
            }

            var hash = Tokens.Hash(header["Bearer ".Length..].Trim());
            var session = await db.SessionTokens
                .SingleOrDefaultAsync(s => s.TokenHash == hash && s.RevokedAt == null, cancellationToken);
            if (session is not null)
            {
                session.RevokedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }

            return TypedResults.NoContent();
        }).RequireAuthorization().WithName("Logout");

        return app;
    }
}
