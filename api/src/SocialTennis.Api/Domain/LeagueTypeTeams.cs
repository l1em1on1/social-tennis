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
}
