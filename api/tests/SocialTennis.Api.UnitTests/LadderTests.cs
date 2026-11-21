using SocialTennis.Api.Domain;

namespace SocialTennis.Api.UnitTests;

/// <summary>Splitting a ladder into Divisions and pairing their Games (#7, ADR-0031).</summary>
public class LadderTests
{
    /// <summary>The Start screen proposes the same (web/src/lib/league.ts, proposeDivisions).</summary>
    [Theory]
    [InlineData(2, 4, new[] { 2 })]
    [InlineData(3, 4, new[] { 3 })]
    [InlineData(4, 4, new[] { 4 })]
    [InlineData(5, 4, new[] { 5 })]
    [InlineData(6, 4, new[] { 3, 3 })]
    [InlineData(7, 4, new[] { 3, 4 })]
    [InlineData(8, 4, new[] { 4, 4 })]
    [InlineData(9, 4, new[] { 4, 5 })]
    [InlineData(10, 4, new[] { 3, 3, 4 })]
    [InlineData(13, 4, new[] { 4, 4, 5 })]
    [InlineData(5, 2, new[] { 2, 3 })]
    [InlineData(7, 3, new[] { 3, 4 })]
    public void A_ladder_splits_into_Divisions_of_about_the_size_bigger_ones_at_the_bottom(int count, int size, int[] divisions) =>
        Assert.Equal(divisions, Ladder.ProposeDivisions(count, size));

    [Fact]
    public void A_proposed_split_holds_everyone_and_no_Division_under_two()
    {
        for (var size = League.MinDivisionSize; size <= League.MaxDivisionSize; size++)
        {
            for (var count = 2; count <= 40; count++)
            {
                var divisions = Ladder.ProposeDivisions(count, size);
                Assert.Equal(count, divisions.Sum());
                Assert.All(divisions, division => Assert.InRange(division, 2, count));
            }
        }
    }

    [Fact]
    public void A_ladder_of_fewer_than_two_has_no_Divisions() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Ladder.ProposeDivisions(1, 4));

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 3)]
    [InlineData(4, 6)]
    [InlineData(5, 10)]
    public void Everyone_in_a_Division_plays_everyone_else_once(int size, int games)
    {
        var pairs = Ladder.RoundRobin(size).ToList();

        Assert.Equal(games, pairs.Count);
        Assert.All(pairs, pair => Assert.True(pair.First < pair.Second));
        Assert.Equal(pairs.Count, pairs.Distinct().Count());
    }
}
