namespace SocialTennis.Api.Domain;

/// <summary>
/// An opaque API session credential, created by redeeming a MagicLinkToken.
/// Stored hashed; the raw value is held server-side by the BFF (HttpOnly
/// cookie) and sent as a Bearer token. Revocable — logout sets RevokedAt.
/// </summary>
public class SessionToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
