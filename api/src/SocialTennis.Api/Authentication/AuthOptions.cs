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
}
