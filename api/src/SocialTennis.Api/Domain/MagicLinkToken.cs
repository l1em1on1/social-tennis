namespace SocialTennis.Api.Domain;

/// <summary>
/// A single-use, expiring login token delivered by link to an email address.
/// Only the SHA-256 hash is stored; the raw token exists solely inside the
/// link. A token belongs to an address, not a User: the User is created when
/// a link is redeemed, so asking for a link creates nothing (#108).
/// </summary>
public class MagicLinkToken
{
    public Guid Id { get; set; }

    /// <summary>The normalised address the link was sent to (<see cref="Authentication.EmailAddress.Normalize"/>).</summary>
    public required string Email { get; set; }

    /// <summary>
    /// SHA-256 hex of the requesting client's key (Authentication/ClientAddress.cs),
    /// so live links can be counted per address and client without storing
    /// the client's address itself.
    /// </summary>
    public required string ClientHash { get; set; }

    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set on redemption — a used token can never redeem again.</summary>
    public DateTimeOffset? UsedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
