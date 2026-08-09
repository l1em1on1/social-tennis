namespace SocialTennis.Api.Domain;

/// <summary>
/// An external identity (provider + subject) attached to a User — the
/// OAuth/OIDC readiness required by ADR-0004. Unused in v1; no provider ships.
/// </summary>
public class ExternalLogin
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Provider key, e.g. "google".</summary>
    public required string Provider { get; set; }

    /// <summary>The provider's stable subject identifier for this User.</summary>
    public required string Subject { get; set; }
}
