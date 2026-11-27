namespace SocialTennis.Api.Domain;

/// <summary>
/// The rules for splitting a ladder into Divisions and making their Games
/// (ADR-0031). Pure, so they are unit-tested; the web app mirrors
/// <see cref="ProposeDivisions"/> for the Start screen.
/// </summary>
public static class Ladder
{
    /// <summary>
    /// The proposed Division sizes for a ladder of <paramref name="count"/>
    /// Competitors with Divisions of about <paramref name="size"/>, top first:
    /// as many Divisions as the count rounds to, at least one and none under 2,
    /// split as evenly as possible, with the bigger Divisions at the bottom.
    /// The Manager can change both the number and the sizes.
    /// </summary>
    /// <example>Size 4: 6 is 3 and 3; 9 is 4 and 5; 10 is 3, 3 and 4; 5 is one Division of 5.</example>
    public static IReadOnlyList<int> ProposeDivisions(int count, int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, League.MinDivisionSize);
        var divisions = Math.Clamp((count + (size / 2)) / size, 1, count / 2);
        var smaller = count / divisions;
        var bigger = count % divisions;
        return [.. Enumerable.Range(0, divisions).Select(index => smaller + (index >= divisions - bigger ? 1 : 0))];
    }

    /// <summary>
    /// The proposed Divisions of a Foursomes ladder of <paramref name="count"/>
    /// Players (#14): as many Divisions of 4 as fit. The rest must be left
    /// out, or more Players found, before it can start: there is no Bye.
    /// </summary>
    public static IReadOnlyList<int> ProposeFoursomes(int count) =>
        [.. Enumerable.Repeat(League.FoursomesDivisionSize, count / League.FoursomesDivisionSize)];

    /// <summary>
    /// A Foursomes Division's 3 Games (#14, ADR-0036), as indexes into its 4
    /// places from the top: 1+2 v 3+4, 1+3 v 2+4 and 1+4 v 2+3. Each Player
    /// partners each of the others once and plays against each twice. Side 1
    /// is the pair with the first place.
    /// </summary>
    public static IReadOnlyList<(int[] First, int[] Second)> FoursomesRotation { get; } =
    [
        ([0, 1], [2, 3]),
        ([0, 2], [1, 3]),
        ([0, 3], [1, 2]),
    ];

    /// <summary>
    /// The Games of a Division of <paramref name="size"/>: every pair once, as
    /// indexes into the Division from its top, the higher place first.
    /// </summary>
    public static IEnumerable<(int First, int Second)> RoundRobin(int size)
    {
        for (var first = 0; first < size; first++)
        {
            for (var second = first + 1; second < size; second++)
            {
                yield return (first, second);
            }
        }
    }
}
