using SocialTennis.Api.Domain;

namespace SocialTennis.Api.UnitTests;

/// <summary>Reading a ladder's Divisions and Bye from its places (#7, ADR-0013).</summary>
public class LadderTests
{
    [Theory]
    [InlineData(1, 4, 1)]
    [InlineData(2, 4, 1)]
    [InlineData(3, 4, 2)]
    [InlineData(4, 4, 2)]
    [InlineData(1, 2, 1)]
    public void An_even_ladder_is_all_Divisions_of_two(int position, int count, int division) =>
        Assert.Equal(division, Ladder.DivisionOf(position, count));

    [Theory]
    [InlineData(1, 5, 1)]
    [InlineData(4, 5, 2)]
    [InlineData(5, 5, null)]
    [InlineData(3, 3, null)]
    public void On_an_odd_ladder_the_last_place_has_the_Bye(int position, int count, int? division) =>
        Assert.Equal(division, Ladder.DivisionOf(position, count));

    [Fact]
    public void A_place_off_the_ladder_is_a_mistake()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Ladder.DivisionOf(0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ladder.DivisionOf(5, 4));
    }

    [Theory]
    [InlineData(LeagueType.SinglesMixed, 2)]
    [InlineData(LeagueType.DoublesMix, 2)]
    [InlineData(LeagueType.Foursomes, 4)]
    public void A_Division_holds_two_Competitors_or_four_in_Foursomes(LeagueType type, int size) =>
        Assert.Equal(size, type.DivisionSize());
}
