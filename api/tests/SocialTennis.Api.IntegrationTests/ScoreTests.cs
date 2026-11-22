using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialTennis.Api.Contracts;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Features.Games.Contracts;
using SocialTennis.Api.Features.Leagues.Contracts;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// Issue #38, part 1: a Player who played a League Game enters its Score, set
/// by set or as a Retirement, checked against the League's format; the League
/// sees it on the Game and the ladder; and who may enter one. The clock is
/// 00:30 on 11 July 2026 in London (<see cref="ClockedTestFactory"/>), in a
/// Stage that started on 1 July.
/// </summary>
public class ScoreTests(ClockedTestFactory factory) : IClassFixture<ClockedTestFactory>
{
    private const string Today = "2026-07-11";

    private static readonly object OneSetOf6 = new { gamesPerSet = 6, setsPerGame = 1, matchTiebreakDecider = false };

    private static readonly object BestOf3MatchTiebreak = new { gamesPerSet = 6, setsPerGame = 3, matchTiebreakDecider = true };

    private async Task<(HttpClient Manager, Guid ClubId)> ClubAsync()
    {
        var admin = await factory.AdminClientAsync();
        var created = await admin.PostAsJsonAsync("/clubs", new { name = $"Score Club {Guid.NewGuid():N}" });
        created.EnsureSuccessStatusCode();
        return (await factory.CreateManagerClientAsync(), (await created.Content.ReadFromJsonAsync<CreateClubResponse>())!.Id);
    }

    private static async Task<Guid> LeagueAsync(HttpClient manager, Guid clubId, object scoreFormat, string type = "SinglesMixed")
    {
        var response = await manager.PostAsJsonAsync($"/clubs/{clubId}/leagues", new
        {
            name = $"Summer {type}",
            type,
            structure = "Divisional",
            startDate = "2026-07-01",
            endDate = "2026-09-30",
            stageLength = new { count = 1, unit = "Months" },
            stageRoll = "Manual",
            scoreFormat,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateLeagueResponse>())!.Id;
    }

    /// <summary>A fresh User with one Player, approved into the Club.</summary>
    private async Task<(HttpClient Client, Guid PlayerId)> MemberAsync(HttpClient manager, Guid clubId, string name)
    {
        var client = await factory.CreateSignedInClientAsync();
        var created = await client.PostAsJsonAsync("/players", new { name });
        var playerId = (await created.Content.ReadFromJsonAsync<CreatePlayerResponse>())!.Id;
        (await client.PutAsync($"/clubs/{clubId}/members/{playerId}", null)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/clubs/{clubId}/members/{playerId}/approval", null)).EnsureSuccessStatusCode();
        return (client, playerId);
    }

    private async Task<(HttpClient Client, Guid PlayerId)> ParticipantAsync(HttpClient manager, Guid clubId, Guid leagueId, string name)
    {
        var member = await MemberAsync(manager, clubId, name);
        (await manager.PutAsync($"/leagues/{leagueId}/participants/{member.PlayerId}", null)).EnsureSuccessStatusCode();
        return member;
    }

    /// <summary>Starts the League with these Teams, top first, in one Division, and the rest left out.</summary>
    private static async Task StartAsync(HttpClient manager, Guid leagueId, Guid[] ladder, Guid[]? leftOut = null) =>
        (await manager.PostAsJsonAsync(
            $"/leagues/{leagueId}/start",
            new { ladder, divisions = new[] { ladder.Length }, leftOut = leftOut ?? [], removed = Array.Empty<Guid>() }))
        .EnsureSuccessStatusCode();

    private static async Task<GetLeagueResponse> ManagedAsync(HttpClient manager, Guid leagueId) =>
        (await manager.GetFromJsonAsync<GetLeagueResponse>($"/leagues/{leagueId}"))!;

    private static Guid TeamOf(GetLeagueResponse league, Guid playerId) =>
        league.Teams.Single(t => t.PlayerIds.Contains(playerId)).Id;

    /// <summary>A started singles League of two, Una over Vic: their Game, and a third Player left out.</summary>
    private async Task<Singles> SinglesAsync(object scoreFormat)
    {
        var (manager, clubId) = await ClubAsync();
        var leagueId = await LeagueAsync(manager, clubId, scoreFormat);
        var una = await ParticipantAsync(manager, clubId, leagueId, "Una");
        var vic = await ParticipantAsync(manager, clubId, leagueId, "Vic");
        var wes = await ParticipantAsync(manager, clubId, leagueId, "Wes");
        var league = await ManagedAsync(manager, leagueId);
        await StartAsync(manager, leagueId, [TeamOf(league, una.PlayerId), TeamOf(league, vic.PlayerId)], [TeamOf(league, wes.PlayerId)]);
        var ladder = (await una.Client.GetFromJsonAsync<GetLeagueLadderResponse>($"/leagues/{leagueId}/ladder"))!;
        return new Singles(manager, clubId, leagueId, ladder.Divisions.Single().Games.Single().Id, una, vic, wes);
    }

    private sealed record Singles(
        HttpClient Manager,
        Guid ClubId,
        Guid LeagueId,
        Guid GameId,
        (HttpClient Client, Guid PlayerId) Una,
        (HttpClient Client, Guid PlayerId) Vic,
        (HttpClient Client, Guid PlayerId) Wes);

    private static object Set(int first, int second, int? firstTiebreak = null, int? secondTiebreak = null) =>
        new { first, second, firstTiebreak, secondTiebreak };

    private static Task<HttpResponseMessage> SubmitAsync(
        HttpClient client, Guid gameId, Guid playerId, object[] sets, string playedOn = Today, int? retirementWinner = null) =>
        client.PostAsJsonAsync($"/games/{gameId}/score", new { playerId, playedOn, sets, retirementWinner });

    private static async Task<GetGameResponse> GameAsync(HttpClient client, Guid gameId) =>
        (await client.GetFromJsonAsync<GetGameResponse>($"/games/{gameId}"))!;

    private static async Task<IDictionary<string, string[]>> ErrorsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors;
    }

    private static async Task<string?> ProblemTypeAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Type;
    }

    [Fact]
    public async Task A_Player_who_played_enters_the_Score_and_the_League_sees_it_on_the_Game_and_the_ladder()
    {
        var game = await SinglesAsync(BestOf3MatchTiebreak);

        var entered = await SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 4), Set(5, 6, 4, 7), Set(10, 7)]);

        Assert.Equal(HttpStatusCode.NoContent, entered.StatusCode);
        // Vic, who played, and Wes, who's in the League but not on the ladder, see it alike.
        foreach (var client in new[] { game.Vic.Client, game.Wes.Client })
        {
            var score = (await GameAsync(client, game.GameId)).Score!;
            Assert.Equal(new DateOnly(2026, 7, 11), score.PlayedOn);
            Assert.Equal(
                [new SetScore(6, 4, null, null, false), new SetScore(5, 6, 4, 7, false), new SetScore(10, 7, null, null, true)],
                score.Sets);
            Assert.Equal(1, score.Winner);
            Assert.False(score.Retired);
            Assert.Equal("Una", score.SubmittedBy);
        }

        var ladder = (await game.Wes.Client.GetFromJsonAsync<GetLeagueLadderResponse>($"/leagues/{game.LeagueId}/ladder"))!;
        var onLadder = ladder.Divisions.Single().Games.Single().Score!;
        Assert.Equal(1, onLadder.Winner);
        Assert.Equal([false, false, true], onLadder.Sets.Select(set => set.MatchTiebreak));
    }

    [Fact]
    public async Task Entering_a_Score_starts_its_history_with_who_entered_it_and_what_they_entered()
    {
        var game = await SinglesAsync(OneSetOf6);

        (await SubmitAsync(game.Vic.Client, game.GameId, game.Vic.PlayerId, [Set(3, 6)])).EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
        var change = await db.ScoreChanges.SingleAsync(c => c.GameId == game.GameId);
        Assert.Equal(ScoreChangeKind.Entered, change.Kind);
        Assert.Equal(game.Vic.PlayerId, change.ByPlayerId);
        Assert.Equal(ClockedTestFactory.Start, change.At);
        Assert.Null(change.Before);
        Assert.Equal((new DateOnly(2026, 7, 11), 2, false), (change.After.PlayedOn, change.After.Winner, change.After.Retired));
        Assert.Equal([new ScoreSet(3, 6, null, null)], change.After.Sets);
    }

    [Fact]
    public async Task Either_Player_of_a_doubles_Team_enters_it_and_the_Game_names_both_on_each_Side()
    {
        var (manager, clubId) = await ClubAsync();
        var leagueId = await LeagueAsync(manager, clubId, OneSetOf6, "DoublesMix");
        var players = new List<(HttpClient Client, Guid PlayerId)>();
        foreach (var name in new[] { "Ann", "Bob", "Cat", "Dan" })
        {
            players.Add(await ParticipantAsync(manager, clubId, leagueId, name));
        }
        var pairs = new List<Guid>();
        foreach (var (first, second) in new[] { (players[0], players[1]), (players[2], players[3]) })
        {
            var paired = await manager.PostAsJsonAsync(
                $"/leagues/{leagueId}/teams", new { firstPlayerId = first.PlayerId, secondPlayerId = second.PlayerId });
            pairs.Add((await paired.Content.ReadFromJsonAsync<PairTeamResponse>())!.Id);
        }
        await StartAsync(manager, leagueId, [.. pairs]);
        var gameId = (await players[3].Client.GetFromJsonAsync<GetLeagueLadderResponse>($"/leagues/{leagueId}/ladder"))!
            .Divisions.Single().Games.Single().Id;

        var before = await GameAsync(players[3].Client, gameId);
        Assert.Equal([players[3].PlayerId], before.YourPlayerIds);
        // In the order they joined the Team, which the still clock leaves to their ids.
        Assert.Equal(
            [(1, "Ann & Bob"), (2, "Cat & Dan")],
            before.Sides.Select(side => (side.Side, string.Join(" & ", side.Players.Select(p => p.Name).Order()))));

        var entered = await SubmitAsync(players[3].Client, gameId, players[3].PlayerId, [Set(2, 6)]);

        Assert.Equal(HttpStatusCode.NoContent, entered.StatusCode);
        var score = (await GameAsync(players[0].Client, gameId)).Score!;
        Assert.Equal((2, "Dan"), (score.Winner, score.SubmittedBy));
    }

    [Fact]
    public async Task A_Retirement_keeps_the_Sets_as_they_stood_and_the_declared_winner()
    {
        var game = await SinglesAsync(BestOf3MatchTiebreak);

        var entered = await SubmitAsync(
            game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 4), Set(2, 1)], retirementWinner: 2);

        Assert.Equal(HttpStatusCode.NoContent, entered.StatusCode);
        var score = (await GameAsync(game.Vic.Client, game.GameId)).Score!;
        Assert.Equal((2, true), (score.Winner, score.Retired));
        Assert.Equal([(6, 4), (2, 1)], score.Sets.Select(set => (set.First, set.Second)));
    }

    [Fact]
    public async Task Sets_the_Leagues_format_doesnt_allow_are_refused_under_the_field_they_concern()
    {
        var oneSet = await SinglesAsync(OneSetOf6);
        var bestOf3 = await SinglesAsync(BestOf3MatchTiebreak);

        Assert.Equal(
            ["A 6–5 set was won on a tiebreak at 5–5: enter its points."],
            (await ErrorsAsync(await SubmitAsync(oneSet.Una.Client, oneSet.GameId, oneSet.Una.PlayerId, [Set(6, 5)])))["sets[0]"]);
        Assert.Equal(
            ["This league plays one set."],
            (await ErrorsAsync(await SubmitAsync(oneSet.Una.Client, oneSet.GameId, oneSet.Una.PlayerId, [Set(6, 4), Set(6, 4)])))["sets"]);
        Assert.Equal(
            ["A match tiebreak is first to 10 points."],
            (await ErrorsAsync(await SubmitAsync(
                bestOf3.Una.Client, bestOf3.GameId, bestOf3.Una.PlayerId, [Set(6, 4), Set(3, 6), Set(6, 2)])))["sets[2]"]);
        Assert.Equal(
            ["These sets finish the game, so it wasn't a retirement."],
            (await ErrorsAsync(await SubmitAsync(
                bestOf3.Una.Client, bestOf3.GameId, bestOf3.Una.PlayerId, [Set(6, 4), Set(6, 3)], retirementWinner: 1)))["sets"]);
        // The request's shape is checked first.
        var shape = await ErrorsAsync(await SubmitAsync(
            oneSet.Una.Client, oneSet.GameId, oneSet.Una.PlayerId, [Set(-1, 6)], retirementWinner: 3));
        Assert.Equal(["Games and points are whole numbers from 0 to 99."], shape["sets"]);
        Assert.Equal(["A retirement's winner is Side 1 or Side 2."], shape["retirementWinner"]);
        Assert.Null((await GameAsync(oneSet.Una.Client, oneSet.GameId)).Score);
    }

    [Theory]
    [InlineData("2026-07-01", null)]
    [InlineData(Today, null)]
    [InlineData("2026-06-30", "Stage 1 started on 1 Jul 2026: the game can't have been played before that.")]
    [InlineData("2026-07-12", "The date played can't be in the future.")]
    public async Task The_date_played_is_from_the_Stages_start_to_today_where_the_Club_is(string playedOn, string? problem)
    {
        var game = await SinglesAsync(OneSetOf6);

        var entered = await SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 1)], playedOn);

        if (problem is null)
        {
            Assert.Equal(HttpStatusCode.NoContent, entered.StatusCode);
        }
        else
        {
            Assert.Equal([problem], (await ErrorsAsync(entered))["playedOn"]);
        }
    }

    [Fact]
    public async Task The_Game_says_what_day_it_is_where_the_Club_is_for_the_date_played()
    {
        var game = await SinglesAsync(OneSetOf6);

        // 23:30 UTC on 10 July is already 11 July in London.
        Assert.Equal(new DateOnly(2026, 7, 11), (await GameAsync(game.Una.Client, game.GameId)).Today);
    }

    [Fact]
    public async Task A_Game_has_one_Score_so_a_second_is_refused()
    {
        var game = await SinglesAsync(OneSetOf6);
        (await SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 1)])).EnsureSuccessStatusCode();

        var again = await SubmitAsync(game.Vic.Client, game.GameId, game.Vic.PlayerId, [Set(1, 6)]);

        Assert.Equal("already-scored", await ProblemTypeAsync(again, HttpStatusCode.Conflict));
        Assert.Equal(1, (await GameAsync(game.Vic.Client, game.GameId)).Score!.Winner);
    }

    [Fact]
    public async Task Two_Players_entering_at_once_get_one_Score_and_one_refusal()
    {
        var game = await SinglesAsync(OneSetOf6);

        var both = await Task.WhenAll(
            SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 1)]),
            SubmitAsync(game.Vic.Client, game.GameId, game.Vic.PlayerId, [Set(1, 6)]));

        Assert.Equal([HttpStatusCode.NoContent, HttpStatusCode.Conflict], both.Select(r => r.StatusCode).Order());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
        Assert.Equal(1, await db.ScoreChanges.CountAsync(c => c.GameId == game.GameId));
    }

    [Fact]
    public async Task Only_a_Player_in_the_Game_enters_its_Score()
    {
        var game = await SinglesAsync(OneSetOf6);
        var sets = new[] { Set(6, 1) };

        // Not the User's Player: as if it weren't there.
        Assert.Equal(HttpStatusCode.NotFound, (await SubmitAsync(game.Wes.Client, game.GameId, game.Una.PlayerId, sets)).StatusCode);
        // In the League, left off the ladder: can see the Game, can't score it.
        Assert.Equal(
            "not-in-game",
            await ProblemTypeAsync(await SubmitAsync(game.Wes.Client, game.GameId, game.Wes.PlayerId, sets), HttpStatusCode.Forbidden));
        // A Club member who isn't in the League can't see the Game at all.
        var outsider = await MemberAsync(game.Manager, game.ClubId, "Xan");
        Assert.Equal(HttpStatusCode.NotFound, (await SubmitAsync(outsider.Client, game.GameId, outsider.PlayerId, sets)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.GetAsync($"/games/{game.GameId}")).StatusCode);
        // A Manager can see it, but overriding is #38's second part: their own Player isn't in it.
        var created = await game.Manager.PostAsJsonAsync("/players", new { name = "Manny" });
        var managersPlayer = (await created.Content.ReadFromJsonAsync<CreatePlayerResponse>())!.Id;
        Assert.Equal(
            "not-in-game",
            await ProblemTypeAsync(await SubmitAsync(game.Manager, game.GameId, managersPlayer, sets), HttpStatusCode.Forbidden));
        // No such Game.
        Assert.Equal(HttpStatusCode.NotFound, (await SubmitAsync(game.Una.Client, Guid.NewGuid(), game.Una.PlayerId, sets)).StatusCode);

        Assert.Null((await GameAsync(game.Una.Client, game.GameId)).Score);
    }

    [Fact]
    public async Task A_Manager_sees_the_Game_holding_no_Player_in_it()
    {
        var game = await SinglesAsync(OneSetOf6);

        var seen = await GameAsync(game.Manager, game.GameId);

        Assert.Empty(seen.YourPlayerIds);
        Assert.Equal(["Una", "Vic"], seen.Sides.Select(side => side.Players.Single().Name));
        Assert.Equal((1, 1, 6, 1, false), (seen.Stage.Number, seen.Division, seen.ScoreFormat.GamesPerSet, seen.ScoreFormat.SetsPerGame, seen.ScoreFormat.MatchTiebreakDecider));
    }
}
