using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SocialTennis.Api.Data;

namespace SocialTennis.Api.Auth;

/// <summary>
/// Authenticates requests carrying "Authorization: Bearer &lt;session token&gt;"
/// against the SessionTokens table — opaque, revocable credentials rather than
/// self-contained JWTs, so logout is immediate and a leaked token dies with
/// its row.
/// </summary>
public class SessionTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TennisDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "SessionToken";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? header = Request.Headers.Authorization;
        if (header is null || !header.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        var hash = Tokens.Hash(header["Bearer ".Length..].Trim());
        var now = DateTimeOffset.UtcNow;
        var session = await db.SessionTokens
            .Include(s => s.User)
            .SingleOrDefaultAsync(s => s.TokenHash == hash && s.RevokedAt == null && s.ExpiresAt > now);

        if (session is null)
        {
            return AuthenticateResult.Fail("Invalid, expired, or revoked session token.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
                new Claim(ClaimTypes.Email, session.User.Email),
            ],
            SchemeName);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
