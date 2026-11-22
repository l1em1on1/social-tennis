using SocialTennis.Api.Domain;

namespace SocialTennis.Api.UnitTests;

/// <summary>A Division's Standings and who would move (#10, ADR-0031).</summary>
public class StandingsTests
{
    private static readonly Guid A = new("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = new("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid C = new("00000000-0000-0000-0000-00000000000c");
    private static readonly Guid D = new("00000000-0000-0000-0000-00000000000d");

    /// <summary>A played Game: the first Team beat the second, by these games.</summary>
    private static StandingsGame Beat(Guid winner, Guid loser, int winnerGames = 6, int loserGames = 2) =>
        new(winner, loser, new StandingsResult(1, winnerGames, loserGames));

    private static StandingsGame Unplayed(Guid first, Guid second) => new(first, second, null);

    private static IEnumerable<(Guid Team, int Rank)> Ranks(IReadOnlyList<Standing> standings) =>
        standings.Select(s => (s.TeamId, s.Rank));

    [Fact]
    public void Teams_rank_by_matches_won_then_games_difference()
    {
        // B and C win two each, B by more games (12–1 against 12–9); A wins one, D none.
        var standings = Standings.Rank(
            [A, B, C, D],
            [Beat(B, A, 6, 0), Beat(C, A, 6, 4), Beat(A, D), Beat(B, D, 6, 1), Beat(C, D, 6, 5), Unplayed(B, C)]);

        Assert.Equal([(B, 1), (C, 2), (A, 3), (D, 4)], Ranks(standings));
        var b = standings[0];
        Assert.Equal((2, 2, 0, 12, 1, false), (b.Played, b.Won, b.Lost, b.GamesFor, b.GamesAgainst, b.Tied));
        Assert.Equal((3, 0, 3), (standings[3].Played, standings[3].Won, standings[3].Lost));
    }

    [Fact]
    public void Two_Teams_level_on_wins_and_games_difference_are_split_by_the_Game_between_them()
    {
        // A and B: two wins each, 15–12. B beat A. C and D: one win each, 12–15. C beat D.
        var standings = Standings.Rank(
            [A, B, C, D],
            [Beat(B, A, 6, 3), Beat(A, C, 6, 3), Beat(A, D, 6, 3), Beat(B, C, 6, 3), Beat(D, B, 6, 3), Beat(C, D, 6, 3)]);

        Assert.Equal([(B, 1), (A, 2), (C, 3), (D, 4)], Ranks(standings));
        Assert.All(standings, s => Assert.False(s.Tied));
    }

    [Fact]
    public void Three_or_more_level_Teams_are_split_by_their_wins_in_the_Games_between_them()
    {
        // A Division of five, every Game 6–2. E wins three, D one. A, B and C win two each, all
        // level on games; among them A won both its Games, B one, C none.
        var e = new Guid("00000000-0000-0000-0000-00000000000e");
        var standings = Standings.Rank(
            [A, B, C, D, e],
            [
                Beat(A, B), Beat(A, C), Beat(B, C), Beat(D, A), Beat(e, A),
                Beat(B, D), Beat(e, B), Beat(C, D), Beat(C, e), Beat(e, D),
            ]);

        Assert.Equal([(e, 1), (A, 2), (B, 3), (C, 4), (D, 5)], Ranks(standings));
        Assert.All(standings, s => Assert.False(s.Tied));
    }

    [Fact]
    public void Level_Teams_whose_Game_between_them_has_no_Score_stay_tied_in_ladder_order()
    {
        // A and B both beat C 6–2; their own Game is unplayed.
        var standings = Standings.Rank([A, B, C], [Beat(A, C), Beat(B, C), Unplayed(A, B)]);

        Assert.Equal([(A, 1), (B, 1), (C, 3)], Ranks(standings));
        Assert.Equal([true, true, false], standings.Select(s => s.Tied));
    }

    [Fact]
    public void A_circle_of_three_level_Teams_is_left_to_the_Manager()
    {
        // A beat B, B beat C, C beat A, all 6–2: level on everything, and on the Games between them.
        var standings = Standings.Rank([A, B, C], [Beat(A, B), Beat(B, C), Beat(C, A)]);

        Assert.Equal([(A, 1), (B, 1), (C, 1)], Ranks(standings));
        Assert.All(standings, s => Assert.True(s.Tied));
    }

    [Theory]
    // One set of 6, won on a tiebreak: the games as recorded.
    [InlineData(6, 1, false, "6-5(7-4)", 6, 5)]
    // Best of three with a Match Tiebreak: the tiebreak is one game to its winner.
    [InlineData(6, 3, true, "6-4 3-6 10-7", 10, 10)]
    [InlineData(6, 3, true, "6-4 3-6 7-10", 9, 11)]
    // A Retirement's games as they stood; an unfinished Match Tiebreak counts nothing.
    [InlineData(6, 3, false, "6-4 2-1", 8, 5)]
    [InlineData(6, 3, true, "6-4 3-6 8-5", 9, 10)]
    public void A_Scores_games_count_as_recorded_with_a_Match_Tiebreak_as_one_game(
        int gamesPerSet, int setsPerGame, bool matchTiebreak, string score, int first, int second)
    {
        var sets = score.Split(' ').Select(set =>
        {
            var parts = set.Split('(', ')');
            var games = parts[0].Split('-').Select(int.Parse).ToArray();
            int[]? points = parts.Length > 1 ? [.. parts[1].Split('-').Select(int.Parse)] : null;
            return new ScoreSet(games[0], games[1], points?[0], points?[1]);
        }).ToList();

        Assert.Equal((first, second), Standings.GamesOf(new ScoreFormat(gamesPerSet, setsPerGame, matchTiebreak), sets));
    }

    [Fact]
    public void The_top_Team_moves_up_and_the_bottom_down_except_at_the_ends_of_the_ladder()
    {
        var standings = Standings.Rank([A, B, C], [Beat(A, B), Beat(A, C), Beat(B, C)]);

        // Division 2 of 3: A up, B stays, C down.
        Assert.Equal(
            [StandingMove.Up, StandingMove.Stays, StandingMove.Down],
            standings.Select(s => Standings.MoveOf(s, standings, 2, 3)));
        // The top Division: its winner stays.
        Assert.Equal(
            [StandingMove.Stays, StandingMove.Stays, StandingMove.Down],
            standings.Select(s => Standings.MoveOf(s, standings, 1, 3)));
        // The bottom Division: its loser stays.
        Assert.Equal(
            [StandingMove.Up, StandingMove.Stays, StandingMove.Stays],
            standings.Select(s => Standings.MoveOf(s, standings, 3, 3)));
        // A ladder of one Division: nobody moves.
        Assert.All(standings, s => Assert.Equal(StandingMove.Stays, Standings.MoveOf(s, standings, 1, 1)));
    }

    [Fact]
    public void A_tie_for_a_place_that_moves_is_undecided_and_one_that_doesnt_isnt()
    {
        // A and B level at the top, C last.
        var top = Standings.Rank([A, B, C], [Beat(A, C), Beat(B, C), Unplayed(A, B)]);
        Assert.Equal([null, null, StandingMove.Down], top.Select(s => Standings.MoveOf(s, top, 2, 3)));
        // In the top Division that tie decides nothing.
        Assert.Equal([StandingMove.Stays, StandingMove.Stays, StandingMove.Down], top.Select(s => Standings.MoveOf(s, top, 1, 3)));

        // B and C level at the bottom of the top Division: who goes down is undecided.
        var bottom = Standings.Rank([A, B, C], [Beat(A, B), Beat(A, C), Unplayed(B, C)]);
        Assert.Equal([StandingMove.Stays, null, null], bottom.Select(s => Standings.MoveOf(s, bottom, 1, 2)));
    }

    [Fact]
    public void Before_any_Game_has_a_Score_nobody_is_moving_yet()
    {
        var standings = Standings.Rank([A, B], [Unplayed(A, B)]);

        Assert.Equal([(A, 1), (B, 1)], Ranks(standings));
        Assert.All(standings, s => Assert.Null(Standings.MoveOf(s, standings, 2, 3)));
    }

    [Fact]
    public void Open_ties_are_the_ones_that_decide_who_moves()
    {
        // A and B level at the top, D last; C third.
        var standings = Standings.Rank([A, B, C, D], [Beat(A, C), Beat(B, D), Beat(A, D), Beat(B, C), Beat(C, D), Unplayed(A, B)]);

        Assert.Equal([[A, B]], Standings.OpenTies(standings, 2, 3));
        // In the top Division a tie for first decides nothing.
        Assert.Empty(Standings.OpenTies(standings, 1, 3));
        // A Division where nothing was played is one tie of everyone, for the Manager to order.
        var unplayed = Standings.Rank([A, B], [Unplayed(A, B)]);
        Assert.Equal([[A, B]], Standings.OpenTies(unplayed, 1, 2));
        Assert.Empty(Standings.OpenTies(unplayed, 1, 1));
    }
}
