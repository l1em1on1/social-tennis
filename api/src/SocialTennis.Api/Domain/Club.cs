namespace SocialTennis.Api.Domain;

/// <summary>
/// A tennis club running leagues and socials. Owns Leagues, Socials, and
/// Players via membership (see CONTEXT.md).
/// </summary>
public class Club
{
    public Guid Id { get; set; }

    public required string Name { get; set; }
}
