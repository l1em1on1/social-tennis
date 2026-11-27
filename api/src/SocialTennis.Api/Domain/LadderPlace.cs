namespace SocialTennis.Api.Domain;

/// <summary>
/// A Competitor's place on a Stage's ladder (ADR-0030), 1 at the top, and its
/// Division there. Divisions are runs of places down the ladder, of sizes the
/// Manager set at the start, so each place stores its own (ADR-0031). There
/// is no Bye on a Divisional ladder.
/// </summary>
public class LadderPlace
{
    public Guid StageId { get; set; }

    public Stage Stage { get; set; } = null!;

    public int Position { get; set; }

    /// <summary>1 at the top. A Division's places are consecutive.</summary>
    public int Division { get; set; }

    /// <summary>With <see cref="StageId"/> and <see cref="TeamId"/>, keeps the Stage and the Team in one League.</summary>
    public Guid LeagueId { get; set; }

    /// <summary>The Competitor: a Team, a Team of one in singles and in Foursomes, where it is the Player (ADR-0036).</summary>
    public Guid TeamId { get; set; }

    public Team Team { get; set; } = null!;
}
