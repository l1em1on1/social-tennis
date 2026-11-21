namespace SocialTennis.Api.Domain;

/// <summary>
/// A Competitor's place on a Stage's ladder (ADR-0030), 1 at the top. Divisions
/// aren't stored: Division d holds the places of its size counting down from
/// the top (places 1 and 2 are Division 1 when a Division is 2), and a single
/// Competitor left over at the bottom has the Bye (ADR-0013).
/// </summary>
public class LadderPlace
{
    public Guid StageId { get; set; }

    public Stage Stage { get; set; } = null!;

    public int Position { get; set; }

    /// <summary>With <see cref="StageId"/> and <see cref="TeamId"/>, keeps the Stage and the Team in one League.</summary>
    public Guid LeagueId { get; set; }

    /// <summary>The Competitor: a Team in the Team-based Types. Foursomes (#14) places Players.</summary>
    public Guid TeamId { get; set; }

    public Team Team { get; set; } = null!;
}
