using SocialTennis.Api.Domain;

namespace SocialTennis.Api.UnitTests;

/// <summary>Foursomes (#14, ADR-0036): the 3-Game rotation of a Division of 4, and its Standings by games banked.</summary>
public class FoursomesTests
{
    private static readonly Guid Ann = new("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Bob = new("00000000-0000-0000-0000-0000000000b0");
    private static readonly Guid Cat = new("00000000-0000-0000-0000-0000000000c0");
    private static readonly Guid Dan = new("00000000-0000-0000-0000-0000000000d0");
    private static readonly Guid[] Division = [Ann, Bob, Cat, Dan];

    /// <summary>The rotation's Games, as Players: 1+2 v 3+4, 1+3 v 2+4, 1+4 v 2+3.</summary>
    private static PairsGame Game(int index, int firstGames, int secondGames, bool notPlayed = false)
    {
        var (first, second) = Ladder.FoursomesRotation[index];
        return new PairsGame(
            [.. first.Select(place => Division[place])],
            [.. second.Select(place => Division[place])],
            notPlayed ? null : new StandingsResult(firstGames > secondGames ? 1 : 2, firstGames, secondGames),
            notPlayed);
    }

    private static IEnumerable<(Guid Player, int Rank, int Banked, bool Tied)> Lines(IReadOnlyList<Standing> standings) =>
        standings.Select(s => (s.TeamId, s.Rank, s.GamesFor, s.Tied));

    [Fact]
    public void Each_Player_partners_each_other_once_and_plays_against_each_twice()
    {
        var pairs = from one in Enumerable.Range(0, 4) from other in Enumerable.Range(0, 4) where one < other select (one, other);
        foreach (var (one, other) in pairs)
        {
            var together = Ladder.FoursomesRotation.Count(game =>
                (game.First.Contains(one) && game.First.Contains(other)) || (game.Second.Contains(one) && game.Second.Contains(other)));
            var against = Ladder.FoursomesRotation.Count(game =>
                (game.First.Contains(one) && game.Second.Contains(other)) || (game.Second.Contains(one) && game.First.Contains(other)));
            Assert.Equal((1, 2), (together, against));
        }

        Assert.All(Ladder.FoursomesRotation, game => Assert.Contains(0, game.First));
    }

    [Theory]
    [InlineData(3, new int[0])]
    [InlineData(4, new[] { 4 })]
    [InlineData(9, new[] { 4, 4 })]
    [InlineData(12, new[] { 4, 4, 4 })]
    public void A_ladder_is_proposed_in_as_many_Divisions_of_4_as_fit(int count, int[] divisions)
    {
        Assert.Equal(divisions, Ladder.ProposeFoursomes(count));
    }

    [Fact]
    public void Each_Player_banks_the_games_their_Side_won_and_ranks_by_them()
    {
        // Ann+Bob 6–2, Ann+Cat 3–6 (Bob+Dan win), Ann+Dan 6–4. Ann 6+3+6 = 15, Bob 6+6+4 = 16, Cat 2+3+4 = 9, Dan 2+6+6 = 14.
        var standings = Standings.RankFoursomes(Division, [Game(0, 6, 2), Game(1, 3, 6), Game(2, 6, 4)]);

        Assert.Equal([(Bob, 1, 16, false), (Ann, 2, 15, false), (Dan, 3, 14, false), (Cat, 4, 9, false)], Lines(standings));
        var bob = standings[0];
        Assert.Equal((3, 2, 1, 16, 11), (bob.Played, bob.Won, bob.Lost, bob.GamesFor, bob.GamesAgainst));
        Assert.Null(bob.Points);
    }

    [Fact]
    public void Players_level_on_games_share_a_rank_with_no_other_tie_break()
    {
        // Ann+Bob 6–4, Ann+Cat 4–6, Ann+Dan 6–4. Ann 16, Bob 6+6+4 = 16, Cat 4+4+4 = 12, Dan 4+6+6 = 16.
        var standings = Standings.RankFoursomes(Division, [Game(0, 6, 4), Game(1, 4, 6), Game(2, 6, 4)]);

        // Ann, Bob and Dan are level for first, in ladder order; the result between any two of them decides nothing.
        Assert.Equal([(Ann, 1, 16, true), (Bob, 1, 16, true), (Dan, 1, 16, true), (Cat, 4, 12, false)], Lines(standings));
        Assert.Equal([[Ann, Bob, Dan]], Standings.OpenTies(standings, number: 2, divisions: 2));
    }

    [Fact]
    public void A_Game_not_played_leaves_the_Divisions_moves_to_the_Manager()
    {
        // Ann+Dan never played: everyone shares first, most games first, and the whole Division is the Manager's to order.
        var standings = Standings.RankFoursomes(Division, [Game(0, 6, 2), Game(1, 3, 6), Game(2, 0, 0, notPlayed: true)]);

        Assert.Equal([(Bob, 1, 12, true), (Ann, 1, 9, true), (Dan, 1, 8, true), (Cat, 1, 5, true)], Lines(standings));
        Assert.Equal([[Bob, Ann, Dan, Cat]], Standings.OpenTies(standings, number: 1, divisions: 2));
        Assert.All(standings, s => Assert.Null(Standings.MoveOf(s, standings, 1, 2)));
    }

    [Fact]
    public void A_Game_with_no_Score_yet_counts_nothing()
    {
        var standings = Standings.RankFoursomes(Division, [Game(0, 6, 2), new PairsGame([Ann, Cat], [Bob, Dan], null, false)]);

        Assert.Equal([(Ann, 1, 6, true), (Bob, 1, 6, true), (Cat, 3, 2, true), (Dan, 3, 2, true)], Lines(standings));
    }

    /// <summary>Ann first on 18; Bob, Cat and Dan level for last on 14: 6–4 three times for Ann's pair.</summary>
    private static IReadOnlyList<Standing> LevelForLast() =>
        Standings.RankFoursomes(Division, [Game(0, 6, 4), Game(1, 6, 4), Game(2, 6, 4)]);

    [Fact]
    public void The_Players_mark_moves_the_one_they_chose_and_the_others_stay()
    {
        var standings = LevelForLast();
        Assert.Equal([Bob, Cat, Dan], Standings.TiedAt(standings, TieMarkPlace.Down, number: 1, divisions: 2));
        Assert.Null(Standings.TiedAt(standings, TieMarkPlace.Up, number: 1, divisions: 2));

        // Unmarked, it waits; Bob marked to go down, he does, though ladder order would send Dan.
        Assert.Equal([[Bob, Cat, Dan]], Standings.OpenTies(standings, 1, 2));
        var marked = Standings.OrderByMarks(standings, up: null, down: Bob);
        Assert.Equal([Ann, Cat, Dan, Bob], marked.Select(s => s.TeamId));
        Assert.Empty(Standings.OpenTies(marked, 1, 2, down: Bob));
        Assert.Equal(
            [StandingMove.Stays, StandingMove.Stays, StandingMove.Stays, StandingMove.Down],
            marked.Select(s => Standings.MoveOf(s, marked, 1, 2, down: Bob)));
        // Ranks stay shared, so the Manager can still order them at the close.
        Assert.Equal([1, 2, 2, 2], marked.Select(s => s.Rank));
    }

    [Fact]
    public void A_tie_for_both_first_and_last_needs_both_marks()
    {
        // A middle Division where all four banked the same: who goes up, and who goes down.
        var standings = Standings.RankFoursomes(Division, [Game(0, 6, 6), Game(1, 6, 6), Game(2, 6, 6)]);
        Assert.Equal([Ann, Bob, Cat, Dan], Standings.TiedAt(standings, TieMarkPlace.Up, 2, 3));
        Assert.Equal([Ann, Bob, Cat, Dan], Standings.TiedAt(standings, TieMarkPlace.Down, 2, 3));

        Assert.Single(Standings.OpenTies(standings, 2, 3, up: Cat));
        var marked = Standings.OrderByMarks(standings, up: Cat, down: Ann);
        Assert.Equal([Cat, Bob, Dan, Ann], marked.Select(s => s.TeamId));
        Assert.Empty(Standings.OpenTies(marked, 2, 3, up: Cat, down: Ann));
        Assert.Equal(
            [StandingMove.Up, StandingMove.Stays, StandingMove.Stays, StandingMove.Down],
            marked.Select(s => Standings.MoveOf(s, marked, 2, 3, up: Cat, down: Ann)));
    }
}
