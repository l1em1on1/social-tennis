namespace SocialTennis.Api.Authentication;

/// <summary>Bound from the "Auth" configuration section.</summary>
public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>How long a magic link stays redeemable.</summary>
    public int MagicLinkLifetimeMinutes { get; set; } = 15;

    /// <summary>How long a session credential stays valid.</summary>
    public int SessionLifetimeDays { get; set; } = 30;

    /// <summary>
    /// Base URL of the web app's verify route; the raw token is appended as
    /// ?token=. The link lands on the BFF, never on this API (ADR-0001).
    /// </summary>
    public string VerifyUrlBase { get; set; } = "http://localhost:3000/auth/verify";

    /// <summary>
    /// Email addresses that are Admins, separated by commas. This is how the
    /// first Admin exists on a fresh deployment: no database step. Whoever
    /// signs in with one of these addresses is an Admin for as long as it is
    /// listed.
    /// </summary>
    public string AdminEmails { get; set; } = "";

    /// <summary>Whether any address is listed: if so the app can never be left with no Admin.</summary>
    public bool HasAdminEmails => AdminEmailSet.Count > 0;

    private HashSet<string>? adminEmailSet;

    private HashSet<string> AdminEmailSet =>
        adminEmailSet ??= AdminEmails
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            // Only addresses count. Terraform passes a placeholder ("none") when
            // the list is empty, since a secret can't be; counting it would let
            // the last granted Admin be demoted (#109).
            .Where(entry => entry.Contains('@'))
            .Select(EmailAddress.Normalize)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Asked on every authenticated request, so the list is parsed once. The
    /// options are bound before first use and don't change while running.
    /// </summary>
    public bool IsAdminEmail(string email) => AdminEmailSet.Contains(EmailAddress.Normalize(email));

    /// <summary>Name of the rate-limiting policy on the magic-link request route.</summary>
    public const string MagicLinkRateLimitPolicy = "magic-link";

    /// <summary>
    /// Ceiling on magic-link requests per minute across all callers; beyond it
    /// the API answers 429.
    /// </summary>
    public int MagicLinkRequestsPerMinute { get; set; } = 60;

    /// <summary>
    /// How many live (unused, unexpired) magic links one address may hold. A
    /// request beyond it is answered 202 like any other, but sends nothing, so
    /// an address can't be mail-bombed and the response still reveals nothing.
    /// </summary>
    public int MaxLiveMagicLinksPerAddress { get; set; } = 5;
}
