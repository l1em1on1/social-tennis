namespace SocialTennis.Api.Domain;

/// <summary>
/// The end-of-Stage movement of a Divisional ladder (#10, ADR-0031; see
/// CONTEXT.md, Relegation). Pure, so it is unit-tested; the close screen
/// shows the same result before the Manager confirms. A Manager's mid-League
/// move is a different code path (#10 part 5): the two must not share one.
/// </summary>
public static class Relegation
{
    /// <summary>
    /// The next Stage's Divisions, top first, from this Stage's, each given in
    /// its final order (Standings, with the Manager's order for ties). Each
    /// Division's first Team moves up one and its last down one; the top of
    /// the ladder and its bottom stay put. A Team moving up starts at the
    /// bottom of the Division above, one moving down at the top of the one
    /// below, so every Division keeps its size. <paramref name="joining"/>
    /// Teams, off the ladder until now, start at the bottom of the bottom one.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<Guid>> Next(
        IReadOnlyList<IReadOnlyList<Guid>> divisions, IReadOnlyList<Guid> joining)
    {
        ArgumentOutOfRangeException.ThrowIfZero(divisions.Count);
        var count = divisions.Count;
        var next = new List<IReadOnlyList<Guid>>();
        for (var index = 0; index < count; index++)
        {
            var division = divisions[index];
            var up = index > 0 ? division[0] : (Guid?)null;
            var down = index < count - 1 ? division[^1] : (Guid?)null;
            var stays = division.Where(team => team != up && team != down);
            List<Guid> placed =
            [
                .. index > 0 ? [divisions[index - 1][^1]] : Array.Empty<Guid>(),
                .. stays,
                .. index < count - 1 ? [divisions[index + 1][0]] : Array.Empty<Guid>(),
            ];
            if (index == count - 1)
            {
                placed.AddRange(joining);
            }

            next.Add(placed);
        }

        return next;
    }
}
