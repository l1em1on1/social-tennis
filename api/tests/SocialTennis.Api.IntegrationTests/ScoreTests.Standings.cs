using System.Net;
using System.Net.Http.Json;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>A League ranked by Bonus points (#214): its ladder, its close screen and both closes count them.</summary>
public partial class ScoreTests
{
    /// <summary>
    /// A Bonus points ladder whose points and wins disagree: in Division 1 Una beats Vic and Wes beats
    /// Vic, but Una v Wes isn't played. Vic, who played both, has 0 + 2 + 1 = 3 points; Una and Wes
    /// have 1 + 1 = 2 each, Una ahead on games difference. By wins Vic would be last; by points Wes is.
    /// In Division 2 Dee, Eve and Fay finish in that order either way.
    /// </summary>
    private async Task<(HttpClient Manager, Guid LeagueId, Guid[] Teams)> PointsAgainstWinsAsync(string stageRoll)
    {
        var (manager, clubId) = await ClubAsync();
        var leagueId = await LeagueAsync(manager, clubId, OneSetOf6, stageRoll: stageRoll, standingsMode: "BonusPoints");
        var players = new List<(HttpClient Client, Guid PlayerId)>();
        foreach (var name in new[] { "Una", "Vic", "Wes", "Dee", "Eve", "Fay" })
        {
            players.Add(await ParticipantAsync(manager, clubId, leagueId, name));
        }
        var league = await ManagedAsync(manager, leagueId);
        var teams = players.Select(p => TeamOf(league, p.PlayerId)).ToArray();
        await StartAsync(manager, leagueId, teams, divisions: [3, 3]);
        var games = (await LadderOfAsync(manager, leagueId)).Divisions.SelectMany(d => d.Games).ToList();
        LadderGame Between(int one, int other) =>
            games.Single(g => new[] { g.FirstTeamId, g.SecondTeamId }.ToHashSet().SetEquals([teams[one], teams[other]]));
        // The winner enters it, its 6 games on its own Side.
        async Task WinAsync(int winner, int loser, int lost)
        {
            var game = Between(winner, loser);
            var set = game.FirstTeamId == teams[winner] ? Set(6, lost) : Set(lost, 6);
            (await SubmitAsync(players[winner].Client, game.Id, players[winner].PlayerId, [set])).EnsureSuccessStatusCode();
        }

        await WinAsync(0, 1, 2);
        await WinAsync(2, 1, 3);
        (await VoidAsync(manager, Between(0, 2).Id)).EnsureSuccessStatusCode();
        await WinAsync(3, 4, 1);
        await WinAsync(3, 5, 1);
        await WinAsync(4, 5, 1);
        Assert.Equal([teams[1], teams[0], teams[2]], (await LadderOfAsync(manager, leagueId)).Divisions[0].Standings.Select(s => s.TeamId));
        return (manager, leagueId, teams);
    }

    [Fact]
    public async Task The_Managers_close_moves_a_Bonus_points_ladder_by_points()
    {
        var (manager, leagueId, teams) = await PointsAgainstWinsAsync("Manual");
        string start = $"{Year}-08-01", end = $"{Year}-08-31";

        // The order by wins isn't the Standings' order.
        Assert.Equal(
            "standings-changed",
            await ProblemTypeAsync(
                await CloseAsync(manager, leagueId, Order([teams[0], teams[2], teams[1]], [teams[3], teams[4], teams[5]]), start: start, end: end),
                HttpStatusCode.Conflict));
        (await CloseAsync(manager, leagueId, Order([teams[1], teams[0], teams[2]], [teams[3], teams[4], teams[5]]), start: start, end: end))
            .EnsureSuccessStatusCode();

        // Wes goes down, not Vic; Dee comes up.
        Assert.Equal(
            [[teams[1], teams[0], teams[3]], [teams[2], teams[4], teams[5]]],
            (await LadderOfAsync(manager, leagueId)).Divisions.Select(d => d.Competitors.Select(c => c.TeamId).ToArray()));
    }

    [Fact]
    public async Task The_auto_roll_job_moves_a_Bonus_points_ladder_by_points()
    {
        var (manager, leagueId, teams) = await PointsAgainstWinsAsync("Automatic");

        factory.Clock.Now = new DateTimeOffset(Year, 7, 31, 23, 5, 0, TimeSpan.Zero);
        await RollAsync();

        var rolled = await LadderOfAsync(manager, leagueId);
        Assert.Equal(2, (int)rolled.Stage!.Number);
        Assert.Equal(
            [[teams[1], teams[0], teams[3]], [teams[2], teams[4], teams[5]]],
            rolled.Divisions.Select(d => d.Competitors.Select(c => c.TeamId).ToArray()));
    }

    [Fact]
    public async Task A_League_ranked_by_Bonus_points_shows_each_Teams_points_and_ranks_by_them()
    {
        var (manager, clubId) = await ClubAsync();
        var leagueId = await LeagueAsync(manager, clubId, OneSetOf6, standingsMode: "BonusPoints");
        var una = await ParticipantAsync(manager, clubId, leagueId, "Una");
        var vic = await ParticipantAsync(manager, clubId, leagueId, "Vic");
        var wes = await ParticipantAsync(manager, clubId, leagueId, "Wes");
        var league = await ManagedAsync(manager, leagueId);
        var (u, v, w) = (TeamOf(league, una.PlayerId), TeamOf(league, vic.PlayerId), TeamOf(league, wes.PlayerId));
        await StartAsync(manager, leagueId, [w, u, v]);
        var games = (await LadderOfAsync(manager, leagueId)).Divisions.Single().Games;
        // The winner's 6 games, on its own Side: a Set's games are Side 1's first.
        async Task WinAsync((HttpClient Client, Guid PlayerId) winner, Guid winnerTeam, Guid loserTeam, int lost)
        {
            var game = games.Single(g => new[] { g.FirstTeamId, g.SecondTeamId }.ToHashSet().SetEquals([winnerTeam, loserTeam]));
            var set = game.FirstTeamId == winnerTeam ? Set(6, lost) : Set(lost, 6);
            (await SubmitAsync(winner.Client, game.Id, winner.PlayerId, [set])).EnsureSuccessStatusCode();
        }

        // Una beats Vic and Wes: 2 + 2 + 1 for playing both = 5. Vic beats Wes: 1 + 2 + 1 = 4. Wes played both, won none: 0 + 2 + 1 = 3.
        await WinAsync(una, u, v, 2);
        await WinAsync(una, u, w, 3);
        await WinAsync(vic, v, w, 4);

        var standings = (await LadderOfAsync(una.Client, leagueId)).Divisions.Single().Standings;
        Assert.Equal([(u, 1, (int?)5), (v, 2, 4), (w, 3, 3)], standings.Select(s => (s.TeamId, s.Rank, s.Points)));
        var close = (await CloseScreenAsync(manager, leagueId)).Divisions.Single().Standings;
        Assert.Equal([5, 4, 3], close.Select(s => s.Points!.Value));
    }

    [Fact]
    public async Task A_Standard_League_shows_no_points()
    {
        var game = await SinglesAsync(OneSetOf6);
        (await SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 2)])).EnsureSuccessStatusCode();

        var standings = (await LadderOfAsync(game.Una.Client, game.LeagueId)).Divisions.Single().Standings;
        Assert.All(standings, s => Assert.Null(s.Points));
        Assert.Equal(StandingsMode.Standard, (await ManagedAsync(game.Manager, game.LeagueId)).StandingsMode);
    }
}
