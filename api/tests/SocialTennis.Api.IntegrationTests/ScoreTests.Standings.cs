using System.Net.Http.Json;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>A League ranked by Bonus points (#214): its ladder and its close screen count them.</summary>
public partial class ScoreTests
{
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
