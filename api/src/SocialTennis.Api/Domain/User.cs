namespace SocialTennis.Api.Domain;

/// <summary>
/// The authentication identity — owns login/session (see CONTEXT.md). A User
/// doesn't need a Player profile; Players arrive in a later ticket.
/// </summary>
public class User
{
    public Guid Id { get; set; }

    /// <summary>Normalized (trimmed, lower-case) email; unique.</summary>
    public required string Email { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// External OAuth/OIDC identities (ADR-0004). Empty in v1 — the table
    /// exists so adding a provider later is additive, not a restructuring.
    /// </summary>
    public List<ExternalLogin> ExternalLogins { get; set; } = [];
}
