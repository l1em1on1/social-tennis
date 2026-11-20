namespace SocialTennis.Api.Domain;

/// <summary>
/// A League-scoped pairing that competes together for the life of the League
/// (CONTEXT.md, Team): one Player in a singles League, two in a doubles one.
/// A <see cref="LeagueType.Foursomes"/> League has none. A Team's Rating is
/// derived from its Players' on read, never stored.
/// </summary>
public class Team
{
    public Guid Id { get; set; }

    public Guid LeagueId { get; set; }

    public League League { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public List<LeagueParticipant> Members { get; set; } = [];
}
