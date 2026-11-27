namespace SocialTennis.Api.Domain;

/// <summary>How many Players a <see cref="Team"/> of each League Type holds (CONTEXT.md, Team).</summary>
public static class LeagueTypeTeams
{
    /// <summary>
    /// 1 for singles, 2 for doubles, and 0 for <see cref="LeagueType.Foursomes"/>,
    /// whose Competitors are individual Players with no fixed Team.
    /// </summary>
    public static int TeamSize(this LeagueType type) => type switch
    {
        LeagueType.SinglesMen or LeagueType.SinglesWomen or LeagueType.SinglesMixed => 1,
        LeagueType.DoublesMen or LeagueType.DoublesWomen or LeagueType.DoublesMix => 2,
        LeagueType.Foursomes => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown League Type."),
    };

    /// <summary>
    /// Whether each participant is a Team of one, made when they join: in
    /// singles, and in <see cref="LeagueType.Foursomes"/>, where the Team of
    /// one is how the ladder, Games and Standings hold an individual Player
    /// (#14, ADR-0036). Not a doubles Team: <see cref="TeamSize"/> stays 0.
    /// </summary>
    public static bool HasTeamsOfOne(this LeagueType type) => type.TeamSize() == 1 || type == LeagueType.Foursomes;
}
