namespace SocialTennis.Api.Domain;

/// <summary>A Player in a League, by an approved Join Request or a Manager's add.</summary>
public class LeagueParticipant
{
    public Guid LeagueId { get; set; }

    public League League { get; set; } = null!;

    public Guid PlayerId { get; set; }

    public Player Player { get; set; } = null!;

    /// <summary>
    /// The request time for an approved request, the add time for a Manager's
    /// add. Breaks ties in the proposed starting ladder (#7, ADR-0013).
    /// </summary>
    public DateTimeOffset JoinedAt { get; set; }

    /// <summary>
    /// The participant's Team (<see cref="LeagueTypeTeams.TeamSize"/>): always
    /// one in a singles League; in a doubles one, none until they are paired;
    /// a Team of one in <see cref="LeagueType.Foursomes"/> (ADR-0036). With <see cref="LeagueId"/>
    /// it references the Team, so the Team is always of this League.
    /// </summary>
    public Guid? TeamId { get; set; }

    public Team? Team { get; set; }
}
