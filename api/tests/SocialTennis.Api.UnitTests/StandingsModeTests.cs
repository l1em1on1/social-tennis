using SocialTennis.Api.Domain;

namespace SocialTennis.Api.UnitTests;

/// <summary>Standings in Bonus points (#214): a point a win, a point a match played, one more for playing them all.</summary>
public class StandingsModeTests
{
    private static readonly Guid A = new("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = new("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid C = new("00000000-0000-0000-0000-00000000000c");
    private static readonly Guid D = new("00000000-0000-0000-0000-00000000000d");

    private static StandingsGame Beat(Guid winner, Guid loser, int winnerGames = 6, int loserGames = 2) =>
        new(winner, loser, new StandingsResult(1, winnerGames, loserGames));

    private static StandingsGame Unplayed(Guid first, Guid second) => new(first, second, null);

    private static IEnumerable<(Guid Team, int Rank, int? Points)> Lines(IReadOnlyList<Standing> standings) =>
        standings.Select(s => (s.TeamId, s.Rank, s.Points));

    [Fact]
    public void Playing_every_Game_can_outrank_a_win_more()
    {
        // A won its two, but never played D: 2 + 2 = 4. B and C won one each and played all three: 1 + 3 + 1 = 5,
        // level on games difference too (10–14), so B, who beat C, is first. D won one of two: 1 + 2 = 3.
        var games = new[] { Beat(A, B), Beat(A, C), Unplayed(A, D), Beat(B, C), Beat(D, B), Beat(C, D) };

        var standard = Standings.Rank([A, B, C, D], games);
        var bonus = Standings.Rank([A, B, C, D], games, StandingsMode.BonusPoints);

        Assert.Equal(A, standard[0].TeamId);
        Assert.Null(standard[0].Points);
        Assert.Equal([(B, 1, (int?)5), (C, 2, 5), (A, 3, 4), (D, 4, 3)], Lines(bonus));
    }

    [Fact]
    public void Level_on_points_go_by_games_difference_then_the_Game_between_them()
    {
        // A and B: a win and a loss each, both played out (1 + 2 + 1 = 4). A by more games; then C and D level on everything, D beat C.
        var byGames = Standings.Rank([A, B, C], [Beat(A, C, 6, 0), Beat(B, A, 6, 4), Beat(C, B, 6, 4)], StandingsMode.BonusPoints);
        Assert.Equal([(A, 1, (int?)4), (B, 2, 4), (C, 3, 4)], Lines(byGames));

        var byHeadToHead = Standings.Rank([C, D], [Beat(D, C, 6, 4)], StandingsMode.BonusPoints);
        Assert.Equal([(D, 1, (int?)3), (C, 2, 2)], Lines(byHeadToHead));
    }

    [Fact]
    public void A_Game_not_played_gives_neither_Side_a_point_nor_the_bonus()
    {
        var standings = Standings.Rank([A, B], [Unplayed(A, B)], StandingsMode.BonusPoints);

        Assert.Equal([(A, 1, (int?)0), (B, 1, 0)], Lines(standings));
        Assert.True(standings.All(s => s.Tied));
    }

    [Fact]
    public void A_Retirement_is_a_match_played_by_both_and_won_by_its_winner()
    {
        // Results carry only the winner and the games: a Retirement counts as any played Game does.
        var standings = Standings.Rank([A, B], [new StandingsGame(A, B, new StandingsResult(2, 3, 2))], StandingsMode.BonusPoints);

        Assert.Equal([(B, 1, (int?)3), (A, 2, 2)], Lines(standings));
    }
}
