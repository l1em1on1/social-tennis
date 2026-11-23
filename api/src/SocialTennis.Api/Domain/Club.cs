namespace SocialTennis.Api.Domain;

/// <summary>
/// A tennis club running leagues and socials. Owns Leagues, Socials, and
/// Players via membership (see CONTEXT.md).
/// </summary>
public class Club
{
    public const int NameMaxLength = 200;

    /// <summary>The first Club's zone, and every Club's until a Club can set its own.</summary>
    public const string DefaultTimeZone = "Europe/London";

    /// <summary>A join code's length: 16 random bytes in base64url, without padding.</summary>
    public const int JoinCodeLength = 22;

    public Guid Id { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// An IANA time zone: the Club's dates (a Stage's, a Game's date played)
    /// are calendar dates there (ADR-0016).
    /// </summary>
    public string TimeZone { get; set; } = DefaultTimeZone;

    /// <summary>
    /// The code in the Club's join link, or none while a Manager has it off
    /// (#222, ADR-0032). Opening the link lets a Player ask to join; a Manager
    /// still approves. Resetting it gives a new code, and the old link stops
    /// working.
    /// </summary>
    public string? JoinCode { get; set; }
}
