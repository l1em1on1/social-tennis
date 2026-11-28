using System.Net;
using System.Net.Http.Json;
using SocialTennis.Api.Features.Games.Contracts;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// Arranging a Game (#9): each Player's free days in the Stage, the days that
/// suit everyone in a Game, and the Date and Court its Players agree.
/// </summary>
public partial class ScoreTests
{
    private static string Day(int month, int day) => $"{Year}-{month:D2}-{day:D2}";

    private static DateOnly On(int month, int day) => new(Year, month, day);

    /// <summary>Sets these dates free any time; every other date follows the usual week.</summary>
    private static Task<HttpResponseMessage> FreeOnAsync((HttpClient Client, Guid PlayerId) player, Guid leagueId, params string[] dates) =>
        SetDaysAsync(player, leagueId, [.. dates.Select(date => (object)new { date, free = true })]);

    private static Task<HttpResponseMessage> SetDaysAsync((HttpClient Client, Guid PlayerId) player, Guid leagueId, object[] days) =>
        player.Client.PutAsJsonAsync($"/leagues/{leagueId}/players/{player.PlayerId}/availability", new { days });

    private static Task<HttpResponseMessage> UsualWeekAsync((HttpClient Client, Guid PlayerId) player, params object[] days) =>
        player.Client.PutAsJsonAsync($"/players/{player.PlayerId}/usual-week", new { days });

    private static DateOnly[] SharedDates(GameAvailability availability) => [.. availability.Shared.Select(day => day.Date)];

    private static DateOnly[] FreeDates(GetAvailabilityResponse availability) => [.. availability.Days.Where(day => day.Free).Select(day => day.Date)];

    private static async Task<GetAvailabilityResponse> FreeDaysOfAsync((HttpClient Client, Guid PlayerId) player, Guid leagueId) =>
        (await player.Client.GetFromJsonAsync<GetAvailabilityResponse>($"/leagues/{leagueId}/players/{player.PlayerId}/availability"))!;

    private static Task<HttpResponseMessage> ArrangeAsync(
        (HttpClient Client, Guid PlayerId) player, Guid gameId, string? date, string? court = null, string? startTime = null) =>
        player.Client.PutAsJsonAsync($"/games/{gameId}/date", new { playerId = player.PlayerId, date, startTime, court });

    [Fact]
    public async Task A_singles_Game_suggests_the_days_both_Players_are_free_from_today()
    {
        // The Stage is July; today is 11 July.
        var game = await SinglesAsync(OneSetOf6);

        (await FreeOnAsync(game.Una, game.LeagueId, Day(7, 5), Day(7, 15), Day(7, 20), Day(7, 12))).EnsureSuccessStatusCode();
        // Vic hasn't said: nothing can suit both, and the Game says why.
        var waiting = (await GameAsync(game.Una.Client, game.GameId)).Availability!;
        Assert.Empty(waiting.Shared);
        Assert.Equal(["Vic"], waiting.NotGiven.Select(name => name.Split(' ')[0]));

        (await FreeOnAsync(game.Vic, game.LeagueId, Day(7, 5), Day(7, 20), Day(7, 15), Day(7, 25))).EnsureSuccessStatusCode();
        // 5 July suits both, but it's gone: only today on counts.
        var shared = (await GameAsync(game.Vic.Client, game.GameId)).Availability!;
        Assert.Equal([On(7, 15), On(7, 20)], SharedDates(shared));
        Assert.Empty(shared.NotGiven);
        // Each sees only their own days; a Manager, or another Player in the League, sees no one's.
        Assert.Equal([On(7, 5), On(7, 12), On(7, 15), On(7, 20)], FreeDates(await FreeDaysOfAsync(game.Una, game.LeagueId)));
        Assert.Null((await GameAsync(game.Manager, game.GameId)).Availability);
        Assert.Null((await GameAsync(game.Wes.Client, game.GameId)).Availability);
        Assert.Equal(HttpStatusCode.NotFound, (await game.Una.Client.GetAsync($"/leagues/{game.LeagueId}/players/{game.Vic.PlayerId}/availability")).StatusCode);

        // Saving again replaces them; a day outside the Stage is refused.
        (await FreeOnAsync(game.Una, game.LeagueId, Day(7, 25))).EnsureSuccessStatusCode();
        Assert.Equal([On(7, 25)], SharedDates((await GameAsync(game.Una.Client, game.GameId)).Availability!));
        Assert.Equal(
            [$"Pick days in Stage 1: from 1 Jul {Year} to 31 Jul {Year}."],
            (await ErrorsAsync(await FreeOnAsync(game.Una, game.LeagueId, Day(8, 1))))["days"]);
    }

    [Fact]
    public async Task A_Foursomes_Game_needs_all_four_Players_free()
    {
        var (manager, leagueId, players, teams) = await FoursomesAsync(Eight[..4]);
        await StartAsync(manager, leagueId, teams, divisions: [4]);
        var game = PairedGame((await LadderOfAsync(manager, leagueId)).Divisions.Single(), teams, 0, 1);
        foreach (var player in players[..3])
        {
            (await FreeOnAsync(player, leagueId, Day(7, 14), Day(7, 21))).EnsureSuccessStatusCode();
        }
        (await FreeOnAsync(players[3], leagueId, Day(7, 21))).EnsureSuccessStatusCode();

        Assert.Equal([On(7, 21)], SharedDates((await GameAsync(players[0].Client, game.Id)).Availability!));
    }

    [Fact]
    public async Task A_doubles_Game_needs_both_pairs_free()
    {
        var (manager, clubId) = await ClubAsync();
        var leagueId = await LeagueAsync(manager, clubId, OneSetOf6, type: "DoublesMix");
        var players = new List<(HttpClient Client, Guid PlayerId)>();
        foreach (var name in new[] { "Ann", "Bob", "Cat", "Dan" })
        {
            players.Add(await ParticipantAsync(manager, clubId, leagueId, name));
        }
        foreach (var (first, second) in new[] { (0, 1), (2, 3) })
        {
            (await manager.PostAsJsonAsync($"/leagues/{leagueId}/teams", new { firstPlayerId = players[first].PlayerId, secondPlayerId = players[second].PlayerId }))
                .EnsureSuccessStatusCode();
        }
        var league = await ManagedAsync(manager, leagueId);
        await StartAsync(manager, leagueId, [TeamOf(league, players[0].PlayerId), TeamOf(league, players[2].PlayerId)]);
        var gameId = (await LadderOfAsync(manager, leagueId)).Divisions.Single().Games.Single().Id;
        foreach (var player in players[..3])
        {
            (await FreeOnAsync(player, leagueId, Day(7, 16))).EnsureSuccessStatusCode();
        }

        // Dan, Cat's partner, hasn't said: no day suits all four.
        var availability = (await GameAsync(players[1].Client, gameId)).Availability!;
        Assert.Empty(availability.Shared);
        Assert.Equal(["Dan"], availability.NotGiven.Select(name => name.Split(' ')[0]));
        (await FreeOnAsync(players[3], leagueId, Day(7, 16))).EnsureSuccessStatusCode();
        Assert.Equal([On(7, 16)], SharedDates((await GameAsync(players[1].Client, gameId)).Availability!));
    }

    [Fact]
    public async Task A_Player_in_the_Game_sets_its_Date_and_Court_and_the_last_change_stands()
    {
        var game = await SinglesAsync(OneSetOf6);

        (await ArrangeAsync(game.Una, game.GameId, Day(7, 20), "  6 - astro  ")).EnsureSuccessStatusCode();
        var seen = await GameAsync(game.Manager, game.GameId);
        Assert.Equal((On(7, 20), "6 - astro"), (seen.Date, seen.Court));
        Assert.StartsWith("Una", seen.DateSetBy);

        // Vic changes it, past the Stage's end (a held-up Stage is still open), without a court.
        (await ArrangeAsync(game.Vic, game.GameId, Day(8, 3))).EnsureSuccessStatusCode();
        var changed = await GameAsync(game.Una.Client, game.GameId);
        Assert.Equal((On(8, 3), (string?)null), (changed.Date, changed.Court));
        Assert.StartsWith("Vic", changed.DateSetBy);
        // The ladder and Home show it too.
        Assert.Equal(On(8, 3), (await LadderOfAsync(game.Una.Client, game.LeagueId)).Divisions.Single().Games.Single().Date);
        var home = (await game.Una.Client.GetFromJsonAsync<GetPlayerGamesResponse>($"/players/{game.Una.PlayerId}/games"))!;
        Assert.Equal(On(8, 3), home.Leagues.Single().Games.Single().Date);

        // Cleared.
        (await ArrangeAsync(game.Una, game.GameId, null)).EnsureSuccessStatusCode();
        Assert.Null((await GameAsync(game.Una.Client, game.GameId)).Date);
    }

    [Fact]
    public async Task Only_its_Players_arrange_a_Game_inside_the_League_and_before_it_is_played()
    {
        var game = await SinglesAsync(OneSetOf6);

        Assert.Equal("not-in-game", await ProblemTypeAsync(await ArrangeAsync(game.Wes, game.GameId, Day(7, 20)), HttpStatusCode.Forbidden));
        Assert.Equal(HttpStatusCode.NotFound, (await ArrangeAsync(game.Una, Guid.NewGuid(), Day(7, 20))).StatusCode);
        Assert.Equal(
            [$"Pick a day from 1 Jul {Year}, when the Stage starts, to 30 Sep {Year}, when the League ends."],
            (await ErrorsAsync(await ArrangeAsync(game.Una, game.GameId, Day(6, 30))))["date"]);
        Assert.True((await ErrorsAsync(await ArrangeAsync(game.Una, game.GameId, Day(10, 1)))).ContainsKey("date"));
        Assert.True((await ErrorsAsync(await ArrangeAsync(game.Una, game.GameId, Day(7, 20), new string('x', 51)))).ContainsKey("court"));

        (await SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 2)])).EnsureSuccessStatusCode();
        Assert.Equal("already-scored", await ProblemTypeAsync(await ArrangeAsync(game.Una, game.GameId, Day(7, 20)), HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Free_days_belong_to_their_Stage()
    {
        var (manager, leagueId, players, teams) = await TwoDivisionsAsync();
        (await FreeOnAsync(players[0], leagueId, Day(7, 20))).EnsureSuccessStatusCode();
        var ladder = await LadderOfAsync(manager, leagueId);
        (await SubmitAsync(players[0].Client, ladder.Divisions[0].Games.Single().Id, players[0].PlayerId, [Set(6, 4), Set(6, 4)])).EnsureSuccessStatusCode();
        (await SubmitAsync(players[2].Client, ladder.Divisions[1].Games.Single().Id, players[2].PlayerId, [Set(6, 4), Set(6, 4)])).EnsureSuccessStatusCode();
        (await CloseAsync(manager, leagueId, Order([teams[0], teams[1]], [teams[2], teams[3]]), start: Day(8, 1), end: Day(8, 31))).EnsureSuccessStatusCode();

        var next = await FreeDaysOfAsync(players[0], leagueId);
        Assert.Equal(2, next.Stage.Number);
        Assert.DoesNotContain(next.Days, day => day.Own || day.Free);
    }

    [Fact]
    public async Task A_Game_not_played_or_in_a_closed_Stage_isnt_arranged_and_shows_no_days()
    {
        var voided = await SinglesAsync(OneSetOf6);
        (await VoidAsync(voided.Manager, voided.GameId)).EnsureSuccessStatusCode();
        var notPlayed = await ArrangeAsync(voided.Una, voided.GameId, Day(7, 20));
        Assert.Equal("not-played", await ProblemTypeAsync(notPlayed, HttpStatusCode.Conflict));

        var (manager, leagueId, players, teams) = await TwoDivisionsAsync();
        (await FreeOnAsync(players[0], leagueId, Day(7, 20))).EnsureSuccessStatusCode();
        (await FreeOnAsync(players[1], leagueId, Day(7, 20))).EnsureSuccessStatusCode();
        var ladder = await LadderOfAsync(manager, leagueId);
        var top = ladder.Divisions[0].Games.Single().Id;
        (await SubmitAsync(players[0].Client, top, players[0].PlayerId, [Set(6, 4), Set(6, 4)])).EnsureSuccessStatusCode();
        (await SubmitAsync(players[2].Client, ladder.Divisions[1].Games.Single().Id, players[2].PlayerId, [Set(6, 4), Set(6, 4)])).EnsureSuccessStatusCode();
        (await CloseAsync(manager, leagueId, Order([teams[0], teams[1]], [teams[2], teams[3]]), start: Day(8, 1), end: Day(8, 31))).EnsureSuccessStatusCode();

        var closed = await ArrangeAsync(players[0], top, Day(7, 20));
        Assert.Equal("stage-closed", await ProblemTypeAsync(closed, HttpStatusCode.Conflict));
        Assert.Null((await GameAsync(players[0].Client, top)).Availability);
    }

    [Fact]
    public async Task Nobody_says_free_days_once_the_League_has_ended()
    {
        var (manager, leagueId, players, teams) = await TwoDivisionsAsync(endDate: Day(7, 31));
        var ladder = await LadderOfAsync(manager, leagueId);
        (await SubmitAsync(players[0].Client, ladder.Divisions[0].Games.Single().Id, players[0].PlayerId, [Set(6, 4), Set(6, 4)])).EnsureSuccessStatusCode();
        (await SubmitAsync(players[2].Client, ladder.Divisions[1].Games.Single().Id, players[2].PlayerId, [Set(6, 4), Set(6, 4)])).EnsureSuccessStatusCode();
        (await CloseAsync(manager, leagueId, Order([teams[0], teams[1]], [teams[2], teams[3]]))).EnsureSuccessStatusCode();

        Assert.Equal("stage-closed", await ProblemTypeAsync(await FreeOnAsync(players[0], leagueId, Day(7, 20)), HttpStatusCode.Conflict));
        Assert.Equal(HttpStatusCode.NotFound, (await players[0].Client.GetAsync($"/leagues/{leagueId}/players/{players[0].PlayerId}/availability")).StatusCode);
    }

    [Fact]
    public async Task Deleting_an_account_deletes_its_free_days()
    {
        var game = await SinglesAsync(OneSetOf6);
        (await FreeOnAsync(game.Una, game.LeagueId, Day(7, 20))).EnsureSuccessStatusCode();
        (await FreeOnAsync(game.Vic, game.LeagueId, Day(7, 20))).EnsureSuccessStatusCode();
        Assert.Equal([On(7, 20)], SharedDates((await GameAsync(game.Vic.Client, game.GameId)).Availability!));

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteUserAsync(await UserIdAsync(game.Una.Client))).StatusCode);

        // Una is a Former player now, with no days: nothing suits both any more.
        var availability = (await GameAsync(game.Vic.Client, game.GameId)).Availability!;
        Assert.Empty(availability.Shared);
        Assert.Equal([SocialTennis.Api.Domain.Player.FormerPlayerName], availability.NotGiven);
    }

    [Fact]
    public async Task A_Stage_held_past_its_deadline_takes_days_up_to_the_Leagues_end()
    {
        var game = await SinglesAsync(OneSetOf6);
        Assert.Equal(On(7, 31), (await FreeDaysOfAsync(game.Una, game.LeagueId)).LastDay);

        // 5 August: July's Stage is past its deadline, still open as its Game has no score.
        factory.Clock.Now = new DateTimeOffset(Year, 8, 5, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(On(9, 30), (await FreeDaysOfAsync(game.Una, game.LeagueId)).LastDay);
        (await FreeOnAsync(game.Una, game.LeagueId, Day(8, 10))).EnsureSuccessStatusCode();
        (await FreeOnAsync(game.Vic, game.LeagueId, Day(8, 10), Day(8, 12))).EnsureSuccessStatusCode();

        Assert.Equal([On(8, 10)], SharedDates((await GameAsync(game.Una.Client, game.GameId)).Availability!));
        Assert.True((await ErrorsAsync(await FreeOnAsync(game.Una, game.LeagueId, Day(10, 1)))).ContainsKey("days"));
    }
}
