using SocialTennis.Api.Domain;

namespace SocialTennis.Api.UnitTests;

/// <summary>
/// Which Scores a League's format allows, and who won (#38). The entry
/// screen's hints give the same answers (web/src/lib/score.ts).
/// </summary>
public class ScoreRulesTests
{
    private static readonly ScoreFormat OneSetOf6 = new(6, 1, false);
    private static readonly ScoreFormat OneSetOf9 = new(9, 1, false);
    private static readonly ScoreFormat BestOf3 = new(6, 3, false);
    private static readonly ScoreFormat BestOf3MatchTiebreak = new(6, 3, true);

    private static ScoreFormat Format(string name) => name switch
    {
        "one set of 6" => OneSetOf6,
        "one set of 9" => OneSetOf9,
        "best of 3" => BestOf3,
        "best of 3, match tiebreak" => BestOf3MatchTiebreak,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    /// <summary>"6-4 6-5(7-3)": Sets, Side 1's games first, with both Sides' tiebreak points in brackets.</summary>
    private static List<ScoreSet> Sets(string score) =>
    [
        .. score.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(set =>
        {
            var parts = set.Split('(', ')');
            var games = parts[0].Split('-').Select(int.Parse).ToArray();
            int[]? points = parts.Length > 1 ? [.. parts[1].Split('-').Select(int.Parse)] : null;
            return new ScoreSet(games[0], games[1], points?[0], points?[1]);
        }),
    ];

    private static ScoreCheck Check(string format, string score, int? retirementWinner = null) =>
        ScoreRules.Check(Format(format), Sets(score), retirementWinner);

    [Theory]
    [InlineData("one set of 6", "6-0", 1)]
    [InlineData("one set of 6", "4-6", 2)]
    [InlineData("one set of 6", "6-5(7-5)", 1)]
    [InlineData("one set of 6", "5-6(8-10)", 2)]
    [InlineData("one set of 6", "6-5(7-0)", 1)]
    [InlineData("one set of 9", "9-7", 1)]
    [InlineData("one set of 9", "8-9(4-7)", 2)]
    [InlineData("best of 3", "6-4 6-3", 1)]
    [InlineData("best of 3", "6-4 3-6 2-6", 2)]
    [InlineData("best of 3", "6-5(9-7) 3-6 6-0", 1)]
    [InlineData("best of 3, match tiebreak", "6-4 3-6 10-7", 1)]
    [InlineData("best of 3, match tiebreak", "6-4 3-6 12-14", 2)]
    [InlineData("best of 3, match tiebreak", "6-1 6-2", 1)]
    public void A_finished_Game_is_won_by_the_Side_that_won_the_Sets_it_needed(string format, string score, int winner)
    {
        var check = Check(format, score);

        Assert.True(check.IsValid, check.Message);
        Assert.Equal(winner, check.Winner);
    }

    [Theory]
    // A set ends at its games, and at one short apiece it goes to a tiebreak.
    [InlineData("one set of 6", "7-5", "sets[0]", "A set ends at 6 games.")]
    [InlineData("one set of 6", "6-6", "sets[0]", "6–6 can't happen: at 5–5 the set goes to a tiebreak.")]
    [InlineData("one set of 6", "6-5", "sets[0]", "A 6–5 set was won on a tiebreak at 5–5: enter its points.")]
    [InlineData("one set of 9", "9-8", "sets[0]", "A 9–8 set was won on a tiebreak at 8–8: enter its points.")]
    [InlineData("one set of 6", "5-5", "sets[0]", "At 5–5 the set goes to a tiebreak: enter its points.")]
    [InlineData("one set of 6", "5-3", "sets[0]", "A set ends at 6 games, or 6–5 on a tiebreak.")]
    // Tiebreak points go with a 6–5 set only, and must have won it.
    [InlineData("one set of 6", "6-4(7-5)", "sets[0]", "Only a set won 6–5 has tiebreak points.")]
    [InlineData("one set of 6", "6-5(5-7)", "sets[0]", "The side that won the tiebreak wins the set.")]
    [InlineData("one set of 6", "6-5(6-4)", "sets[0]", "A tiebreak is first to 7 points.")]
    [InlineData("one set of 6", "6-5(7-6)", "sets[0]", "A tiebreak needs a 2-point lead, like 7–5 or 8–6.")]
    [InlineData("one set of 6", "6-5(10-6)", "sets[0]", "Past 7, a tiebreak ends as soon as someone is 2 points clear.")]
    [InlineData("one set of 6", "5-5(7-5)", "sets[0]", "A finished tiebreak wins the set 6–5.")]
    // A match tiebreak is first to 10, 2 clear, and only where the League plays one.
    [InlineData("best of 3, match tiebreak", "6-4 3-6 6-3", "sets[2]", "A match tiebreak is first to 10 points.")]
    [InlineData("best of 3, match tiebreak", "6-4 3-6 10-9", "sets[2]", "A match tiebreak needs a 2-point lead, like 10–8 or 11–9.")]
    [InlineData("best of 3, match tiebreak", "6-4 3-6 13-9", "sets[2]", "Past 10, a match tiebreak ends as soon as someone is 2 points clear.")]
    [InlineData("best of 3, match tiebreak", "6-4 3-6 10-7(7-5)", "sets[2]", "A match tiebreak's points are its score: it has no tiebreak of its own.")]
    [InlineData("best of 3", "6-4 3-6 10-7", "sets[2]", "A set ends at 6 games.")]
    // As many sets as it takes: a third only at one set all.
    [InlineData("one set of 6", "6-4 6-3", "sets", "This league plays one set.")]
    [InlineData("best of 3", "6-4 6-3 6-2", "sets[2]", "The game was already won: there's no set after that.")]
    [InlineData("best of 3", "6-4 3-6 6-2 6-1", "sets", "This league plays best of three sets.")]
    [InlineData("best of 3", "6-4", "sets", "The game isn't finished: enter the rest of the sets, or record a retirement.")]
    [InlineData("best of 3", "6-4 3-6", "sets", "The game isn't finished: enter the rest of the sets, or record a retirement.")]
    [InlineData("best of 3", "", "sets", "Enter the score of each set.")]
    public void A_Score_the_format_doesnt_allow_says_why_and_where(string format, string score, string? field, string? message)
    {
        var check = Check(format, score);

        Assert.Equal(field, check.Field);
        Assert.Equal(message, check.Message);
    }

    [Theory]
    [InlineData("one set of 6", "4-2", 2)]
    [InlineData("one set of 6", "0-0", 1)]
    [InlineData("one set of 6", "5-5", 1)]
    [InlineData("one set of 6", "5-5(4-3)", 2)]
    [InlineData("one set of 6", "5-5(7-7)", 1)]
    [InlineData("best of 3", "6-4", 2)]
    [InlineData("best of 3", "6-4 2-1", 2)]
    [InlineData("best of 3", "6-4 3-6 1-0", 1)]
    [InlineData("best of 3, match tiebreak", "6-4 3-6 8-5", 2)]
    [InlineData("best of 3, match tiebreak", "6-4 3-6 10-10", 2)]
    public void A_Retirement_keeps_the_Sets_as_they_stood_and_is_won_by_the_declared_Side(string format, string score, int winner)
    {
        var check = Check(format, score, retirementWinner: winner);

        Assert.True(check.IsValid, check.Message);
        Assert.Equal(winner, check.Winner);
    }

    [Theory]
    [InlineData("one set of 6", "6-2", "sets", "These sets finish the game, so it wasn't a retirement.")]
    [InlineData("best of 3", "6-4 6-3", "sets", "These sets finish the game, so it wasn't a retirement.")]
    [InlineData("best of 3", "6-4 6-3 1-0", "sets[2]", "The game was already won: there's no set after that.")]
    [InlineData("one set of 6", "7-5", "sets[0]", "A set ends at 6 games.")]
    [InlineData("one set of 6", "6-5", "sets[0]", "A 6–5 set was won on a tiebreak at 5–5: enter its points.")]
    [InlineData("best of 3", "2-1 6-4", "sets[0]", "A set ends at 6 games, or 6–5 on a tiebreak.")]
    [InlineData("one set of 6", "5-5(7-5)", "sets[0]", "A finished tiebreak wins the set 6–5.")]
    [InlineData("one set of 6", "5-5(10-6)", "sets[0]", "Past 7, a tiebreak ends as soon as someone is 2 points clear.")]
    [InlineData("best of 3, match tiebreak", "6-4 3-6 14-9", "sets[2]", "Past 10, a match tiebreak ends as soon as someone is 2 points clear.")]
    public void A_Retirement_can_only_leave_its_last_Set_unfinished(string format, string score, string field, string message)
    {
        var check = Check(format, score, retirementWinner: 1);

        Assert.Equal(field, check.Field);
        Assert.Equal(message, check.Message);
    }

    [Fact]
    public void A_Retirement_needs_both_tiebreak_points_or_neither()
    {
        var check = ScoreRules.Check(OneSetOf6, [new ScoreSet(5, 5, 3, null)], retirementWinner: 1);

        Assert.Equal(("sets[0]", "Enter both sides' tiebreak points."), (check.Field, check.Message));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void Only_the_third_Set_of_a_best_of_three_that_allows_it_is_a_Match_Tiebreak(int index, bool matchTiebreak)
    {
        Assert.Equal(matchTiebreak, BestOf3MatchTiebreak.IsMatchTiebreak(index));
        Assert.False(BestOf3.IsMatchTiebreak(index));
        Assert.False(new ScoreFormat(6, 1, true).IsMatchTiebreak(index));
    }
}
