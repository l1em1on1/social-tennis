using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;

namespace SocialTennis.Api.Features.Auth;

/// <summary>
/// Logout revokes the presented session server-side — the credential is dead
/// even if a copy of the cookie survives somewhere.
/// </summary>
public static class Logout
{
    public static async Task<Results<NoContent, UnauthorizedHttpResult>> HandleAsync(
        HttpContext context,
        TennisDbContext db,
        CancellationToken cancellationToken)
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
    }
}
