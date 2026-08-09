namespace SocialTennis.Api.Domain;

/// <summary>
/// A single-use, expiring login token delivered to the User by link. Only the
/// SHA-256 hash is stored; the raw token exists solely inside the link.
/// </summary>
public class MagicLinkToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set on redemption — a used token can never redeem again.</summary>
    public DateTimeOffset? UsedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
