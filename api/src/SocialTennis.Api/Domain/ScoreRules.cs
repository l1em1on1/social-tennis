namespace SocialTennis.Api.Domain;

/// <summary>
/// Whether a Score's Sets are possible in a League's <see cref="ScoreFormat"/>,
/// and who won (#38; see CONTEXT.md: Set, Match Tiebreak, Retirement). Pure,
/// so they are unit-tested; the web app's <c>lib/score.ts</c> mirrors them,
/// message for message, for the entry screen's hints.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A Set of N games is won at N with a lead of two or more, or N–(N−1)
/// on a tiebreak at (N−1)–(N−1): first to 7 points and 2 clear, ending as soon
/// as someone is.</item>
/// <item>A Match Tiebreak is first to 10 points and 2 clear, the same way.</item>
/// <item>The Game is one Set, or best of three: no Set after it is won.</item>
/// <item>A Retirement keeps the Sets as they stood: its last Set may be
/// unfinished, the Sets mustn't already finish the Game, and its winner is
/// declared.</item>
/// </list>
/// </remarks>
public static class ScoreRules
{
    public const int TiebreakPoints = 7;

    public const int MatchTiebreakPoints = 10;

    /// <summary>
    /// Checks <paramref name="sets"/>, in the order played, against
    /// <paramref name="format"/>. <paramref name="retirementWinner"/> is the
    /// declared winning Side of a Retirement, or null for a Game played out.
    /// </summary>
    /// <returns>The winning Side, or the problem keyed by the request's wire name (<c>sets</c>, <c>sets[1]</c>).</returns>
    public static ScoreCheck Check(ScoreFormat format, IReadOnlyList<ScoreSet> sets, int? retirementWinner)
    {
        if (sets.Count == 0)
        {
            return ScoreCheck.Problem("sets", "Enter the score of each set.");
        }

        if (sets.Count > format.SetsPerGame)
        {
            return ScoreCheck.Problem(
                "sets", format.SetsPerGame == 1 ? "This league plays one set." : "This league plays best of three sets.");
        }

        var toWin = format.SetsPerGame == 1 ? 1 : 2;
        int[] won = [0, 0];
        for (var index = 0; index < sets.Count; index++)
        {
            if (won.Max() == toWin)
            {
                return ScoreCheck.Problem($"sets[{index}]", "The game was already won: there's no set after that.");
            }

            var set = CheckSet(format, sets[index], format.IsMatchTiebreak(index));
            var lastOfRetirement = retirementWinner is not null && index == sets.Count - 1;
            switch (set.State)
            {
                case SetState.Won:
                    won[set.Winner - 1]++;
                    break;
                case SetState.Unfinished when lastOfRetirement:
                    break;
                default:
                    return ScoreCheck.Problem($"sets[{index}]", set.Message!);
            }
        }

        var finished = won.Max() == toWin;
        if (retirementWinner is { } declared)
        {
            return finished
                ? ScoreCheck.Problem("sets", "These sets finish the game, so it wasn't a retirement.")
                : ScoreCheck.Won(declared);
        }

        return finished
            ? ScoreCheck.Won(won[0] == toWin ? 1 : 2)
            : ScoreCheck.Problem("sets", "The game isn't finished: enter the rest of the sets, or record a retirement.");
    }

    private enum SetState
    {
        Won,
        Unfinished,
        Impossible,
    }

    private readonly record struct SetResult(SetState State, int Winner, string? Message)
    {
        public static SetResult WonBy(int side) => new(SetState.Won, side, null);

        public static SetResult Unfinished(string message) => new(SetState.Unfinished, 0, message);

        public static SetResult Impossible(string message) => new(SetState.Impossible, 0, message);
    }

    private static SetResult CheckSet(ScoreFormat format, ScoreSet set, bool matchTiebreak)
    {
        var (high, low) = (Math.Max(set.First, set.Second), Math.Min(set.First, set.Second));
        var leader = set.First > set.Second ? 1 : 2;
        var hasPoints = set.FirstTiebreak is not null || set.SecondTiebreak is not null;
        if (matchTiebreak)
        {
            if (hasPoints)
            {
                return SetResult.Impossible("A match tiebreak's points are its score: it has no tiebreak of its own.");
            }

            return Race(high, low, MatchTiebreakPoints, "match tiebreak") is { } problem ? problem : SetResult.WonBy(leader);
        }

        var games = format.GamesPerSet;
        if (high > games)
        {
            return SetResult.Impossible($"A set ends at {games} games.");
        }

        if (high == games && low == games - 1)
        {
            if (set.FirstTiebreak is not { } first || set.SecondTiebreak is not { } second)
            {
                return SetResult.Impossible(
                    $"A {games}–{games - 1} set was won on a tiebreak at {games - 1}–{games - 1}: enter its points.");
            }

            if (Race(Math.Max(first, second), Math.Min(first, second), TiebreakPoints, "tiebreak") is { } problem)
            {
                // Unfinished points can't have won the Set.
                return SetResult.Impossible(problem.Message!);
            }

            return (first > second ? 1 : 2) == leader
                ? SetResult.WonBy(leader)
                : SetResult.Impossible("The side that won the tiebreak wins the set.");
        }

        if (high == games - 1 && low == games - 1)
        {
            if (!hasPoints)
            {
                return SetResult.Unfinished($"At {games - 1}–{games - 1} the set goes to a tiebreak: enter its points.");
            }

            if (set.FirstTiebreak is not { } first || set.SecondTiebreak is not { } second)
            {
                return SetResult.Impossible("Enter both sides' tiebreak points.");
            }

            // A tiebreak still being played, in a Retirement; a finished one wins the Set.
            return Race(Math.Max(first, second), Math.Min(first, second), TiebreakPoints, "tiebreak") is { } problem
                ? problem
                : SetResult.Impossible($"A finished tiebreak wins the set {games}–{games - 1}.");
        }

        if (hasPoints)
        {
            return SetResult.Impossible($"Only a set won {games}–{games - 1} has tiebreak points.");
        }

        if (high == games)
        {
            return low <= games - 2
                ? SetResult.WonBy(leader)
                : SetResult.Impossible($"{games}–{games} can't happen: at {games - 1}–{games - 1} the set goes to a tiebreak.");
        }

        return SetResult.Unfinished($"A set ends at {games} games, or {games}–{games - 1} on a tiebreak.");
    }

    /// <summary>A first-to-<paramref name="target"/>, 2-clear race: null once won, or why it isn't.</summary>
    private static SetResult? Race(int high, int low, int target, string name)
    {
        if (high < target)
        {
            return SetResult.Unfinished($"A {name} is first to {target} points.");
        }

        if (high > target && high - low > 2)
        {
            return SetResult.Impossible($"Past {target}, a {name} ends as soon as someone is 2 points clear.");
        }

        return high - low < 2
            ? SetResult.Unfinished($"A {name} needs a 2-point lead, like {target}–{target - 2} or {target + 1}–{target - 1}.")
            : null;
    }
}

/// <summary>What <see cref="ScoreRules.Check"/> found: the winning Side, or one problem with where it is.</summary>
public sealed record ScoreCheck(int Winner, string? Field, string? Message)
{
    public bool IsValid => Field is null;

    public static ScoreCheck Won(int side) => new(side, null, null);

    public static ScoreCheck Problem(string field, string message) => new(0, field, message);
}
