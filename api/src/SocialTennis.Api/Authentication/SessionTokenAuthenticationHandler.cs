using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Authentication;

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
    TennisDbContext db,
    IOptions<AuthOptions> authOptions,
    TimeProvider clock)
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
        var now = clock.GetUtcNow();
        var session = await db.SessionTokens
            .Include(s => s.User)
            .SingleOrDefaultAsync(s => s.TokenHash == hash && s.RevokedAt == null && s.ExpiresAt > now);

        if (session is null)
        {
            return AuthenticateResult.Fail("Invalid, expired, or revoked session token.");
        }

        // Roles are read on every request, so granting or removing one takes
        // effect at once, not at the next sign-in.
        var roles = session.User.Roles;
        if (authOptions.Value.IsAdminEmail(session.User.Email))
        {
            roles |= UserRoles.Admin;
        }

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new(ClaimTypes.Email, session.User.Email),
        ];
        if (roles.HasFlag(UserRoles.Admin))
        {
            // An Admin can do everything a Manager can.
            claims.Add(new Claim(ClaimTypes.Role, SessionRoles.Admin));
            claims.Add(new Claim(ClaimTypes.Role, SessionRoles.Manager));
        }
        else if (roles.HasFlag(UserRoles.Manager))
        {
            claims.Add(new Claim(ClaimTypes.Role, SessionRoles.Manager));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
