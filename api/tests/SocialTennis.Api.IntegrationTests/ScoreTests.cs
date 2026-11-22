using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
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
/// 00:30 on 11 July next year in London (<see cref="ClockedTestFactory"/>), in
/// a Stage that started on 1 July.
/// </summary>
public class ScoreTests(ClockedTestFactory factory) : IClassFixture<ClockedTestFactory>
{
    private static readonly int Year = ClockedTestFactory.Start.Year;

    private static readonly string Today = $"{Year}-07-11";

    // xUnit makes a new instance per test: every test starts at the factory's start, whatever one before it moved.
    private readonly DateTimeOffset start = factory.Clock.Now = ClockedTestFactory.Start;

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
            startDate = $"{Year}-07-01",
            endDate = $"{Year}-09-30",
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
    private static async Task StartAsync(HttpClient manager, Guid leagueId, Guid[] ladder, Guid[]? leftOut = null, int[]? divisions = null) =>
        (await manager.PostAsJsonAsync(
            $"/leagues/{leagueId}/start",
            new { ladder, divisions = divisions ?? [ladder.Length], leftOut = leftOut ?? [], removed = Array.Empty<Guid>() }))
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
        HttpClient client, Guid gameId, Guid playerId, object[] sets, string? playedOn = null, int? retirementWinner = null) =>
        client.PostAsJsonAsync($"/games/{gameId}/score", new { playerId, playedOn = playedOn ?? Today, sets, retirementWinner });

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
            Assert.Equal(new DateOnly(Year, 7, 11), score.PlayedOn);
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
        Assert.Equal((new DateOnly(Year, 7, 11), 2, false), (change.After.PlayedOn, change.After.Winner, change.After.Retired));
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
        var bestOf3NoMatchTiebreak = await SinglesAsync(new { gamesPerSet = 6, setsPerGame = 3, matchTiebreakDecider = false });

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
        // Where the League plays a full third Set, a first-to-10 decider isn't one.
        Assert.Equal(
            ["A set ends at 6 games."],
            (await ErrorsAsync(await SubmitAsync(
                bestOf3NoMatchTiebreak.Una.Client, bestOf3NoMatchTiebreak.GameId, bestOf3NoMatchTiebreak.Una.PlayerId, [Set(6, 4), Set(3, 6), Set(10, 7)])))["sets[2]"]);
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

    /// <summary>Days from the Stage's first day, 1 July; today is the 11th.</summary>
    [Theory]
    [InlineData(0, null)]
    [InlineData(10, null)]
    [InlineData(-1, "before")]
    [InlineData(11, "future")]
    public async Task The_date_played_is_from_the_Stages_start_to_today_where_the_Club_is(int day, string? refused)
    {
        var game = await SinglesAsync(OneSetOf6);
        var playedOn = new DateOnly(Year, 7, 1).AddDays(day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var problem = refused switch
        {
            "before" => $"Stage 1 started on 1 Jul {Year}: the game can't have been played before that.",
            "future" => "The date played can't be in the future.",
            _ => null,
        };

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
        Assert.Equal(new DateOnly(Year, 7, 11), (await GameAsync(game.Una.Client, game.GameId)).Today);
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

    private static Task<HttpResponseMessage> CorrectAsync(
        HttpClient client, Guid gameId, Guid playerId, uint version, object[] sets, string? playedOn = null, int? retirementWinner = null) =>
        client.PutAsJsonAsync($"/games/{gameId}/score", new { playerId, playedOn = playedOn ?? Today, sets, retirementWinner, version });

    private static Task<HttpResponseMessage> OverrideAsync(
        HttpClient client, Guid gameId, uint version, object[] sets, string? playedOn = null, int? retirementWinner = null) =>
        client.PutAsJsonAsync($"/games/{gameId}/score/override", new { playedOn = playedOn ?? Today, sets, retirementWinner, version });

    private async Task CloseStageAsync(Guid leagueId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
        await db.Stages.Where(s => s.LeagueId == leagueId).ExecuteUpdateAsync(set => set.SetProperty(s => s.ClosedAt, factory.Clock.Now));
    }

    private async Task<int> ChangesAsync(Guid gameId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TennisDbContext>().ScoreChanges.CountAsync(c => c.GameId == gameId);
    }

    [Fact]
    public async Task Either_Side_corrects_the_Score_and_every_change_is_kept_with_what_it_was()
    {
        var game = await SinglesAsync(BestOf3MatchTiebreak);
        (await SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 4), Set(6, 3)])).EnsureSuccessStatusCode();
        var entered = (await GameAsync(game.Vic.Client, game.GameId)).Score!;

        // Vic, the other Side, makes it three Sets, the third a Match Tiebreak he won.
        factory.Clock.Now = start.AddHours(1);
        var corrected = await CorrectAsync(
            game.Vic.Client, game.GameId, game.Vic.PlayerId, entered.Version, [Set(6, 4), Set(3, 6), Set(7, 10)], $"{Year}-07-10");

        Assert.Equal(HttpStatusCode.NoContent, corrected.StatusCode);
        var afterVic = await GameAsync(game.Una.Client, game.GameId);
        Assert.Equal((2, new DateOnly(Year, 7, 10), "Una"), (afterVic.Score!.Winner, afterVic.Score.PlayedOn, afterVic.Score.SubmittedBy));
        Assert.Equal([false, false, true], afterVic.Score.Sets.Select(set => set.MatchTiebreak));
        Assert.NotEqual(entered.Version, afterVic.Score.Version);

        // Una cuts it back to the one Set played before a Retirement she says she won.
        factory.Clock.Now = start.AddHours(2);
        (await CorrectAsync(game.Una.Client, game.GameId, game.Una.PlayerId, afterVic.Score.Version, [Set(6, 4)], retirementWinner: 1))
            .EnsureSuccessStatusCode();

        var seen = await GameAsync(game.Wes.Client, game.GameId);
        Assert.Equal((1, true), (seen.Score!.Winner, seen.Score.Retired));
        Assert.Equal([(6, 4)], seen.Score.Sets.Select(set => (set.First, set.Second)));
        // The latest first: who, when, and from what to what.
        Assert.Equal(
            [
                (ScoreChangeKind.Corrected, "Una", start.AddHours(2), 3, 1),
                (ScoreChangeKind.Corrected, "Vic", start.AddHours(1), 2, 3),
                (ScoreChangeKind.Entered, "Una", start, 0, 2),
            ],
            seen.History.Select(c => (c.Kind, c.By!, c.At, c.Before?.Sets.Count ?? 0, c.After.Sets.Count)));
        Assert.Equal((new DateOnly(Year, 7, 11), new DateOnly(Year, 7, 10)), (seen.History[1].Before!.PlayedOn, seen.History[1].After.PlayedOn));
        Assert.Equal((false, true), (seen.History[0].Before!.Retired, seen.History[0].After.Retired));
        Assert.Equal("Europe/London", seen.TimeZone);
    }

    [Fact]
    public async Task A_correction_made_from_an_older_version_is_refused_and_changes_nothing()
    {
        var game = await SinglesAsync(OneSetOf6);
        (await SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 4)])).EnsureSuccessStatusCode();
        var opened = (await GameAsync(game.Una.Client, game.GameId)).Score!.Version;
        (await CorrectAsync(game.Vic.Client, game.GameId, game.Vic.PlayerId, opened, [Set(4, 6)])).EnsureSuccessStatusCode();

        // Una saves what she opened before Vic's correction.
        var stale = await CorrectAsync(game.Una.Client, game.GameId, game.Una.PlayerId, opened, [Set(6, 2)]);

        Assert.Equal("score-changed", await ProblemTypeAsync(stale, HttpStatusCode.Conflict));
        Assert.Equal(2, (await GameAsync(game.Una.Client, game.GameId)).Score!.Winner);
        Assert.Equal(2, await ChangesAsync(game.GameId));
    }

    [Fact]
    public async Task Two_corrections_from_the_same_version_at_once_save_one_and_refuse_the_other()
    {
        var game = await SinglesAsync(OneSetOf6);
        (await SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 4)])).EnsureSuccessStatusCode();
        var opened = (await GameAsync(game.Una.Client, game.GameId)).Score!.Version;

        var both = await Task.WhenAll(
            CorrectAsync(game.Una.Client, game.GameId, game.Una.PlayerId, opened, [Set(6, 2)]),
            CorrectAsync(game.Vic.Client, game.GameId, game.Vic.PlayerId, opened, [Set(2, 6)]));

        Assert.Equal([HttpStatusCode.NoContent, HttpStatusCode.Conflict], both.Select(r => r.StatusCode).Order());
        Assert.Equal(2, await ChangesAsync(game.GameId));
    }

    [Fact]
    public async Task A_correction_that_says_what_the_Score_already_does_records_nothing()
    {
        var game = await SinglesAsync(OneSetOf6);
        (await SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 4)])).EnsureSuccessStatusCode();
        var score = (await GameAsync(game.Una.Client, game.GameId)).Score!;

        var same = await CorrectAsync(game.Vic.Client, game.GameId, game.Vic.PlayerId, score.Version, [Set(6, 4)]);

        Assert.Equal(HttpStatusCode.NoContent, same.StatusCode);
        Assert.Equal(1, await ChangesAsync(game.GameId));
        Assert.Equal(score.Version, (await GameAsync(game.Una.Client, game.GameId)).Score!.Version);
    }

    [Fact]
    public async Task Corrections_are_checked_like_entries()
    {
        var game = await SinglesAsync(OneSetOf6);
        (await SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 4)])).EnsureSuccessStatusCode();
        var version = (await GameAsync(game.Una.Client, game.GameId)).Score!.Version;

        var errors = await ErrorsAsync(await CorrectAsync(game.Vic.Client, game.GameId, game.Vic.PlayerId, version, [Set(6, 5)], $"{Year}-07-12"));

        Assert.Equal(["A 6–5 set was won on a tiebreak at 5–5: enter its points."], errors["sets[0]"]);
        Assert.Equal(["The date played can't be in the future."], errors["playedOn"]);
        Assert.Equal(1, await ChangesAsync(game.GameId));
    }

    [Fact]
    public async Task Only_a_Player_in_the_Game_corrects_a_Score_it_has()
    {
        var game = await SinglesAsync(OneSetOf6);
        var sets = new[] { Set(6, 1) };

        Assert.Equal("not-scored", await ProblemTypeAsync(await CorrectAsync(game.Una.Client, game.GameId, game.Una.PlayerId, 0, sets), HttpStatusCode.Conflict));
        (await SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 4)])).EnsureSuccessStatusCode();
        var version = (await GameAsync(game.Una.Client, game.GameId)).Score!.Version;

        Assert.Equal(
            "not-in-game",
            await ProblemTypeAsync(await CorrectAsync(game.Wes.Client, game.GameId, game.Wes.PlayerId, version, sets), HttpStatusCode.Forbidden));
        Assert.Equal(HttpStatusCode.NotFound, (await CorrectAsync(game.Wes.Client, game.GameId, game.Una.PlayerId, version, sets)).StatusCode);
        var outsider = await MemberAsync(game.Manager, game.ClubId, "Xan");
        Assert.Equal(HttpStatusCode.NotFound, (await CorrectAsync(outsider.Client, game.GameId, outsider.PlayerId, version, sets)).StatusCode);
        Assert.Equal(1, await ChangesAsync(game.GameId));
    }

    [Fact]
    public async Task Once_the_Stage_closes_its_Players_cant_enter_or_correct_a_Score_and_a_Manager_overrides_it()
    {
        var scored = await SinglesAsync(OneSetOf6);
        var unscored = await SinglesAsync(OneSetOf6);
        (await SubmitAsync(scored.Una.Client, scored.GameId, scored.Una.PlayerId, [Set(6, 4)])).EnsureSuccessStatusCode();
        var version = (await GameAsync(scored.Una.Client, scored.GameId)).Score!.Version;
        factory.Clock.Now = start.AddDays(1);
        await CloseStageAsync(scored.LeagueId);
        await CloseStageAsync(unscored.LeagueId);

        Assert.True((await GameAsync(scored.Una.Client, scored.GameId)).Stage.Closed);
        Assert.Equal(
            "stage-closed",
            await ProblemTypeAsync(await CorrectAsync(scored.Vic.Client, scored.GameId, scored.Vic.PlayerId, version, [Set(4, 6)]), HttpStatusCode.Conflict));
        Assert.Equal(
            "stage-closed",
            await ProblemTypeAsync(await SubmitAsync(unscored.Una.Client, unscored.GameId, unscored.Una.PlayerId, [Set(6, 1)]), HttpStatusCode.Conflict));

        // The Manager, holding no Player, overrides it after the close.
        factory.Clock.Now = start.AddDays(2);
        var overridden = await OverrideAsync(scored.Manager, scored.GameId, version, [Set(4, 6)]);

        Assert.Equal(HttpStatusCode.NoContent, overridden.StatusCode);
        var seen = await GameAsync(scored.Una.Client, scored.GameId);
        Assert.Equal(2, seen.Score!.Winner);
        Assert.Equal((ScoreChangeKind.Overridden, (string?)null, start.AddDays(2)), (seen.History[0].Kind, seen.History[0].By, seen.History[0].At));
        Assert.Equal(1, seen.History[0].Before!.Winner);

        // A Manager holding Players is named by their first.
        await scored.Manager.PostAsJsonAsync("/players", new { name = "Manny" });
        factory.Clock.Now = start.AddDays(3);
        (await OverrideAsync(scored.Manager, scored.GameId, seen.Score.Version, [Set(6, 0)])).EnsureSuccessStatusCode();
        Assert.Equal("Manny", (await GameAsync(scored.Vic.Client, scored.GameId)).History[0].By);
    }

    [Fact]
    public async Task Only_a_Manager_overrides_a_Score_the_Game_has_from_its_current_version()
    {
        var game = await SinglesAsync(OneSetOf6);
        Assert.Equal("not-scored", await ProblemTypeAsync(await OverrideAsync(game.Manager, game.GameId, 0, [Set(6, 1)]), HttpStatusCode.Conflict));
        (await SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 4)])).EnsureSuccessStatusCode();
        var version = (await GameAsync(game.Una.Client, game.GameId)).Score!.Version;

        Assert.Equal(HttpStatusCode.Forbidden, (await OverrideAsync(game.Una.Client, game.GameId, version, [Set(6, 1)])).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await OverrideAsync(game.Manager, Guid.NewGuid(), version, [Set(6, 1)])).StatusCode);
        Assert.Equal(
            ["The date played can't be in the future."],
            (await ErrorsAsync(await OverrideAsync(game.Manager, game.GameId, version, [Set(6, 1)], $"{Year}-07-12")))["playedOn"]);
        (await CorrectAsync(game.Vic.Client, game.GameId, game.Vic.PlayerId, version, [Set(4, 6)])).EnsureSuccessStatusCode();
        Assert.Equal(
            "score-changed",
            await ProblemTypeAsync(await OverrideAsync(game.Manager, game.GameId, version, [Set(6, 1)]), HttpStatusCode.Conflict));
        Assert.Equal(2, await ChangesAsync(game.GameId));
    }

    [Fact]
    public async Task The_migration_gives_each_entry_so_far_the_User_who_holds_its_Player()
    {
        var game = await SinglesAsync(OneSetOf6);
        (await SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 4)])).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
        var unasUser = await db.Players.Where(p => p.Id == game.Una.PlayerId).Select(p => p.UserId).SingleAsync();
        var someoneElse = await db.Players.Where(p => p.Id == game.Vic.PlayerId).Select(p => p.UserId).SingleAsync();
        await db.ScoreChanges.Where(c => c.GameId == game.GameId).ExecuteUpdateAsync(set => set.SetProperty(c => c.ByUserId, someoneElse));

        foreach (var sql in new SocialTennis.Api.Migrations.ScoreCorrections().UpOperations.OfType<SqlOperation>().Select(o => o.Sql))
        {
            await db.Database.ExecuteSqlRawAsync(sql);
        }

        Assert.Equal(unasUser, await db.ScoreChanges.Where(c => c.GameId == game.GameId).Select(c => c.ByUserId).SingleAsync());
    }

    [Fact]
    public async Task Rolling_the_migration_back_drops_the_overrides_which_name_no_Player_and_keeps_the_rest()
    {
        var game = await SinglesAsync(OneSetOf6);
        (await SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 4)])).EnsureSuccessStatusCode();
        var version = (await GameAsync(game.Una.Client, game.GameId)).Score!.Version;
        factory.Clock.Now = start.AddHours(1);
        (await OverrideAsync(game.Manager, game.GameId, version, [Set(4, 6)])).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();

        // Down's own SQL, which runs before it makes ByPlayerId required again.
        foreach (var sql in new SocialTennis.Api.Migrations.ScoreCorrections().DownOperations.OfType<SqlOperation>().Select(o => o.Sql))
        {
            await db.Database.ExecuteSqlRawAsync(sql);
        }

        var left = await db.ScoreChanges.Where(c => c.GameId == game.GameId).Select(c => new { c.Kind, c.ByPlayerId }).ToListAsync();
        Assert.Equal([(ScoreChangeKind.Entered, (Guid?)game.Una.PlayerId)], left.Select(c => (c.Kind, c.ByPlayerId)));
    }

    [Fact]
    public async Task Each_Divisions_Standings_follow_its_Scores_and_say_who_would_move()
    {
        // Two Divisions of two: Ann over Bea, then Cal over Dee.
        var (manager, clubId) = await ClubAsync();
        var leagueId = await LeagueAsync(manager, clubId, BestOf3MatchTiebreak);
        var players = new List<(HttpClient Client, Guid PlayerId)>();
        foreach (var name in new[] { "Ann", "Bea", "Cal", "Dee" })
        {
            players.Add(await ParticipantAsync(manager, clubId, leagueId, name));
        }
        var league = await ManagedAsync(manager, leagueId);
        var teams = players.Select(p => TeamOf(league, p.PlayerId)).ToArray();
        await StartAsync(manager, leagueId, teams, divisions: [2, 2]);
        async Task<GetLeagueLadderResponse> LadderAsync() =>
            (await players[0].Client.GetFromJsonAsync<GetLeagueLadderResponse>($"/leagues/{leagueId}/ladder"))!;
        var before = await LadderAsync();

        // Nothing played: level, and nobody moving yet.
        Assert.All(before.Divisions, d => Assert.All(d.Standings, s => Assert.Equal((1, true, (StandingMove?)null), (s.Rank, s.Tied, s.Move))));

        // Bea beats Ann in a Match Tiebreak; Cal beats Dee in straight sets.
        var top = before.Divisions[0].Games.Single().Id;
        var bottom = before.Divisions[1].Games.Single().Id;
        (await SubmitAsync(players[1].Client, top, players[1].PlayerId, [Set(4, 6), Set(6, 3), Set(7, 10)])).EnsureSuccessStatusCode();
        (await SubmitAsync(players[2].Client, bottom, players[2].PlayerId, [Set(6, 1), Set(6, 2)])).EnsureSuccessStatusCode();
        var played = await LadderAsync();

        var first = played.Divisions[0].Standings;
        Assert.Equal([(teams[1], 1, StandingMove.Stays), (teams[0], 2, StandingMove.Down)], first.Select(s => (s.TeamId, s.Rank, s.Move)));
        // The Match Tiebreak is one game: Bea 6+3+1 = 10 to Ann's 4+6+0 = 10.
        Assert.Equal((1, 1, 0, 10, 10), (first[0].Played, first[0].Won, first[0].Lost, first[0].GamesFor, first[0].GamesAgainst));
        Assert.Equal(
            [(teams[2], StandingMove.Up), (teams[3], StandingMove.Stays)],
            played.Divisions[1].Standings.Select(s => (s.TeamId, s.Move)));

        // Corrected, Dee won: the Standings follow.
        var version = (await GameAsync(players[2].Client, bottom)).Score!.Version;
        (await CorrectAsync(players[3].Client, bottom, players[3].PlayerId, version, [Set(1, 6), Set(2, 6)])).EnsureSuccessStatusCode();
        Assert.Equal(
            [(teams[3], StandingMove.Up), (teams[2], StandingMove.Stays)],
            (await LadderAsync()).Divisions[1].Standings.Select(s => (s.TeamId, s.Move)));
    }

    private static Task<HttpResponseMessage> RecordAsync(HttpClient client, Guid gameId, object[] sets, string? playedOn = null, int? retirementWinner = null) =>
        client.PostAsJsonAsync($"/games/{gameId}/score/record", new { playedOn = playedOn ?? Today, sets, retirementWinner });

    private static Task<HttpResponseMessage> VoidAsync(HttpClient client, Guid gameId) => client.PutAsync($"/games/{gameId}/not-played", null);

    private static Task<HttpResponseMessage> UnvoidAsync(HttpClient client, Guid gameId) => client.DeleteAsync($"/games/{gameId}/not-played");

    /// <summary>Two Divisions of two, Ann over Bea, then Cal over Dee, best of three with a Match Tiebreak.</summary>
    private async Task<(HttpClient Manager, Guid LeagueId, List<(HttpClient Client, Guid PlayerId)> Players, Guid[] Teams)> TwoDivisionsAsync()
    {
        var (manager, clubId) = await ClubAsync();
        var leagueId = await LeagueAsync(manager, clubId, BestOf3MatchTiebreak);
        var players = new List<(HttpClient Client, Guid PlayerId)>();
        foreach (var name in new[] { "Ann", "Bea", "Cal", "Dee" })
        {
            players.Add(await ParticipantAsync(manager, clubId, leagueId, name));
        }
        var league = await ManagedAsync(manager, leagueId);
        var teams = players.Select(p => TeamOf(league, p.PlayerId)).ToArray();
        await StartAsync(manager, leagueId, teams, divisions: [2, 2]);
        return (manager, leagueId, players, teams);
    }

    [Fact]
    public async Task A_Manager_records_the_Score_of_a_Game_nobody_scored_and_it_names_no_Player()
    {
        var game = await SinglesAsync(OneSetOf6);

        var recorded = await RecordAsync(game.Manager, game.GameId, [Set(6, 3)]);

        Assert.Equal(HttpStatusCode.NoContent, recorded.StatusCode);
        var seen = await GameAsync(game.Una.Client, game.GameId);
        Assert.Equal((1, true, (string?)null), (seen.Score!.Winner, seen.Score.RecordedByManager, seen.Score.SubmittedBy));
        Assert.Equal((ScoreChangeKind.Recorded, (string?)null), (seen.History.Single().Kind, seen.History.Single().By));
        // It's a Score like any other: a Player in the Game can correct it while the Stage is open.
        (await CorrectAsync(game.Vic.Client, game.GameId, game.Vic.PlayerId, seen.Score.Version, [Set(3, 6)])).EnsureSuccessStatusCode();
        // But not recorded twice, nor by a Player, nor for a Game there isn't.
        Assert.Equal("already-scored", await ProblemTypeAsync(await RecordAsync(game.Manager, game.GameId, [Set(6, 1)]), HttpStatusCode.Conflict));
        Assert.Equal(HttpStatusCode.Forbidden, (await RecordAsync(game.Una.Client, game.GameId, [Set(6, 1)])).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await RecordAsync(game.Manager, Guid.NewGuid(), [Set(6, 1)])).StatusCode);
    }

    [Fact]
    public async Task A_recorded_Score_is_checked_like_an_entered_one_and_names_the_Managers_own_Player()
    {
        var game = await SinglesAsync(OneSetOf6);
        Assert.Equal(
            ["The date played can't be in the future."],
            (await ErrorsAsync(await RecordAsync(game.Manager, game.GameId, [Set(6, 1)], $"{Year}-07-12")))["playedOn"]);
        await game.Manager.PostAsJsonAsync("/players", new { name = "Manny" });

        (await RecordAsync(game.Manager, game.GameId, [Set(6, 1)])).EnsureSuccessStatusCode();

        var seen = await GameAsync(game.Vic.Client, game.GameId);
        Assert.Equal(("Manny", true), (seen.Score!.SubmittedBy, seen.Score.RecordedByManager));
        Assert.Equal("Manny", seen.History.Single().By);
    }

    [Fact]
    public async Task A_Manager_marks_a_Game_not_played_so_it_takes_no_Score_and_can_undo_it_while_the_Stage_is_open()
    {
        var game = await SinglesAsync(OneSetOf6);

        Assert.Equal(HttpStatusCode.NoContent, (await VoidAsync(game.Manager, game.GameId)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await VoidAsync(game.Manager, game.GameId)).StatusCode);

        Assert.True((await GameAsync(game.Una.Client, game.GameId)).NotPlayed);
        var ladder = (await game.Una.Client.GetFromJsonAsync<GetLeagueLadderResponse>($"/leagues/{game.LeagueId}/ladder"))!;
        Assert.True(ladder.Divisions.Single().Games.Single().NotPlayed);
        Assert.Empty(ladder.Awaiting.UnscoredGameIds);
        Assert.Equal("not-played", await ProblemTypeAsync(await SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 1)]), HttpStatusCode.Conflict));
        Assert.Equal("not-played", await ProblemTypeAsync(await RecordAsync(game.Manager, game.GameId, [Set(6, 1)]), HttpStatusCode.Conflict));

        Assert.Equal(HttpStatusCode.NoContent, (await UnvoidAsync(game.Manager, game.GameId)).StatusCode);
        Assert.False((await GameAsync(game.Una.Client, game.GameId)).NotPlayed);
        (await SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 1)])).EnsureSuccessStatusCode();
        // A Game with a Score was played.
        Assert.Equal("already-scored", await ProblemTypeAsync(await VoidAsync(game.Manager, game.GameId), HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Only_a_Manager_marks_a_Game_not_played_and_not_once_its_Stage_has_closed()
    {
        var game = await SinglesAsync(OneSetOf6);
        Assert.Equal(HttpStatusCode.Forbidden, (await VoidAsync(game.Una.Client, game.GameId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UnvoidAsync(game.Una.Client, game.GameId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await VoidAsync(game.Manager, Guid.NewGuid())).StatusCode);
        (await VoidAsync(game.Manager, game.GameId)).EnsureSuccessStatusCode();
        await CloseStageAsync(game.LeagueId);

        Assert.Equal("stage-closed", await ProblemTypeAsync(await UnvoidAsync(game.Manager, game.GameId), HttpStatusCode.Conflict));
        var other = await SinglesAsync(OneSetOf6);
        await CloseStageAsync(other.LeagueId);
        Assert.Equal("stage-closed", await ProblemTypeAsync(await VoidAsync(other.Manager, other.GameId), HttpStatusCode.Conflict));
        Assert.Equal("stage-closed", await ProblemTypeAsync(await RecordAsync(other.Manager, other.GameId, [Set(6, 1)]), HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Marking_a_Game_not_played_while_a_Player_scores_it_leaves_one_or_the_other()
    {
        var game = await SinglesAsync(OneSetOf6);

        await Task.WhenAll(VoidAsync(game.Manager, game.GameId), SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 1)]));

        var seen = await GameAsync(game.Una.Client, game.GameId);
        Assert.True(seen.NotPlayed ^ seen.Score is not null);
    }

    [Fact]
    public async Task The_ladder_says_what_holds_up_the_Stage_and_once_its_deadline_has_passed_that_its_held_up()
    {
        var (manager, leagueId, players, teams) = await TwoDivisionsAsync();
        async Task<GetLeagueLadderResponse> LadderAsync() =>
            (await players[0].Client.GetFromJsonAsync<GetLeagueLadderResponse>($"/leagues/{leagueId}/ladder"))!;
        var before = await LadderAsync();
        var (top, bottom) = (before.Divisions[0].Games.Single().Id, before.Divisions[1].Games.Single().Id);

        // Nothing played: both Games wait, and each Division is a tie for its place that moves.
        Assert.False(before.HeldUp);
        Assert.Equal([top, bottom], before.Awaiting.UnscoredGameIds);
        Assert.Equal([(1, teams[0], teams[1]), (2, teams[2], teams[3])], before.Awaiting.Ties.Select(t => (t.Division, t.TeamIds[0], t.TeamIds[1])));

        // Bea beats Ann; Cal v Dee isn't played, so it waits on nothing but their tie.
        (await SubmitAsync(players[1].Client, top, players[1].PlayerId, [Set(4, 6), Set(6, 3), Set(7, 10)])).EnsureSuccessStatusCode();
        (await VoidAsync(manager, bottom)).EnsureSuccessStatusCode();
        var settled = await LadderAsync();
        Assert.Empty(settled.Awaiting.UnscoredGameIds);
        Assert.Equal([(2, (IReadOnlyList<Guid>)[teams[2], teams[3]])], settled.Awaiting.Ties.Select(t => (t.Division, t.TeamIds)));

        // Stage 1 runs to 31 July: still open at 23:59 that day in London, held up a minute later.
        factory.Clock.Now = new DateTimeOffset(Year, 7, 31, 22, 59, 0, TimeSpan.Zero);
        Assert.False((await LadderAsync()).HeldUp);
        factory.Clock.Now = new DateTimeOffset(Year, 7, 31, 23, 0, 0, TimeSpan.Zero);
        Assert.True((await LadderAsync()).HeldUp);
    }

    [Fact]
    public async Task The_migration_gives_each_Score_so_far_its_Players_User_and_rolling_it_back_drops_the_Managers()
    {
        var entered = await SinglesAsync(OneSetOf6);
        var recorded = await SinglesAsync(OneSetOf6);
        (await SubmitAsync(entered.Una.Client, entered.GameId, entered.Una.PlayerId, [Set(6, 4)])).EnsureSuccessStatusCode();
        (await RecordAsync(recorded.Manager, recorded.GameId, [Set(6, 4)])).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
        var unasUser = await db.Players.Where(p => p.Id == entered.Una.PlayerId).Select(p => p.UserId).SingleAsync();
        var someoneElse = await db.Players.Where(p => p.Id == entered.Vic.PlayerId).Select(p => p.UserId).SingleAsync();
        await db.Scores.Where(s => s.GameId == entered.GameId).ExecuteUpdateAsync(set => set.SetProperty(s => s.SubmittedByUserId, someoneElse));

        foreach (var sql in new SocialTennis.Api.Migrations.StageSettling().UpOperations.OfType<SqlOperation>().Select(o => o.Sql))
        {
            await db.Database.ExecuteSqlRawAsync(sql);
        }

        Assert.Equal(unasUser, await db.Scores.Where(s => s.GameId == entered.GameId).Select(s => s.SubmittedByUserId).SingleAsync());

        foreach (var sql in new SocialTennis.Api.Migrations.StageSettling().DownOperations.OfType<SqlOperation>().Select(o => o.Sql))
        {
            await db.Database.ExecuteSqlRawAsync(sql);
        }

        Assert.True(await db.Scores.AnyAsync(s => s.GameId == entered.GameId));
        Assert.False(await db.Scores.AnyAsync(s => s.GameId == recorded.GameId));
        Assert.False(await db.ScoreChanges.AnyAsync(c => c.GameId == recorded.GameId));
    }
}
