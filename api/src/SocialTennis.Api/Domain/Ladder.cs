namespace SocialTennis.Api.Domain;

/// <summary>
/// Reading a Stage's ladder (ADR-0013, ADR-0030): its places are numbered from
/// 1 at the top, and its Divisions are runs of a fixed size counting down from
/// there. Pure, so the rules are unit-tested.
/// </summary>
public static class Ladder
{
    /// <summary>Competitors per Division: 2 for the Team-based Types, 4 for Foursomes (#14).</summary>
    public static int DivisionSize(this LeagueType type) => type == LeagueType.Foursomes ? 4 : 2;

    /// <summary>
    /// The Division of the Competitor in <paramref name="position"/> on a ladder
    /// of <paramref name="count"/>, or null for the Bye: the single Competitor
    /// left over at the bottom when a Division of 2 can't be filled.
    /// </summary>
    public static int? DivisionOf(int position, int count, int size = 2)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(position, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, count);
        var divisions = count / size;
        var division = (position + size - 1) / size;
        return division <= divisions ? division : null;
    }
}
