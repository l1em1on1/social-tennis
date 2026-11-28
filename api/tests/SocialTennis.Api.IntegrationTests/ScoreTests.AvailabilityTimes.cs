using System.Net;
using System.Net.Http.Json;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Games.Contracts;
using SocialTennis.Api.Features.Leagues.Contracts;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// Times of day (#276): a usual week for each Player, a date set differently,
/// the times everyone in a Game shares, and the agreed start time. The Stage
/// is July and today is 11 July; the year moves, so weekdays are worked out.
/// </summary>
public partial class ScoreTests
{
    private static TimeOnly At(int hour, int minute = 0) => new(hour, minute);

    /// <summary>The weekday of a July date, as the API spells it.</summary>
    private static string WeekdayOf(int day) => Weekdays.Of(On(7, day)).ToString();

    private static object Usual(int julyDay, string? from = null, string? to = null) => new { weekday = WeekdayOf(julyDay), from, to };

    private static object Own(int julyDay, bool free = true, string? from = null, string? to = null) => new { date = Day(7, julyDay), free, from, to };

    private static async Task<UsualWeek> UsualWeekOfAsync((HttpClient Client, Guid PlayerId) player) =>
        (await player.Client.GetFromJsonAsync<UsualWeek>($"/players/{player.PlayerId}/usual-week"))!;

    private static SharedTime Shared(int julyDay, TimeOnly? from = null, TimeOnly? to = null) => new(On(7, julyDay), from, to);

    [Fact]
    public async Task A_usual_week_stands_for_every_date_the_Player_hasnt_set()
    {
        var game = await SinglesAsync(OneSetOf6);

        // Una: the weekday of 15 July in the evening, the weekday of 18 July any time.
        (await UsualWeekAsync(game.Una, Usual(18), Usual(15, "18:00", "22:00"))).EnsureSuccessStatusCode();
        var week = await UsualWeekOfAsync(game.Una);
        // Monday first, whatever order they were sent in.
        Assert.Equal(
            new[] { new UsualWeekDay(Weekdays.Of(On(7, 15)), At(18), At(22)), new UsualWeekDay(Weekdays.Of(On(7, 18)), null, null) }.OrderBy(day => day.Weekday),
            week.Days);

        var days = (await FreeDaysOfAsync(game.Una, game.LeagueId)).Days;
        Assert.Equal(31, days.Count);
        Assert.Equal(new AvailabilityDay(On(7, 22), false, true, At(18), At(22)), days.Single(day => day.Date == On(7, 22)));
        Assert.Equal(new AvailabilityDay(On(7, 25), false, true, null, null), days.Single(day => day.Date == On(7, 25)));
        Assert.Equal(new AvailabilityDay(On(7, 16), false, false, null, null), days.Single(day => day.Date == On(7, 16)));

        // Vic has said nothing: not given. With a usual week of his own, he has.
        Assert.Equal(["Vic"], (await GameAsync(game.Una.Client, game.GameId)).Availability!.NotGiven.Select(name => name.Split(' ')[0]));
        (await UsualWeekAsync(game.Vic, Usual(15, "19:00", "23:00"))).EnsureSuccessStatusCode();
        var availability = (await GameAsync(game.Vic.Client, game.GameId)).Availability!;
        Assert.Empty(availability.NotGiven);
        // That weekday from today on, at the time both share; 8 July is gone, and Vic isn't free on the other.
        Assert.Equal([Shared(15, At(19), At(22)), Shared(22, At(19), At(22)), Shared(29, At(19), At(22))], availability.Shared);

        // Saved again, it replaces the week; none clears it, and Una hasn't given any more.
        (await UsualWeekAsync(game.Una, Usual(15))).EnsureSuccessStatusCode();
        Assert.Equal([Shared(15, At(19), At(23)), Shared(22, At(19), At(23)), Shared(29, At(19), At(23))], (await GameAsync(game.Vic.Client, game.GameId)).Availability!.Shared);
        (await UsualWeekAsync(game.Una)).EnsureSuccessStatusCode();
        Assert.Empty((await UsualWeekOfAsync(game.Una)).Days);
        Assert.Equal(["Una"], (await GameAsync(game.Vic.Client, game.GameId)).Availability!.NotGiven.Select(name => name.Split(' ')[0]));
    }

    [Fact]
    public async Task A_date_set_differently_wins_over_the_usual_week_and_can_go_back_to_it()
    {
        var game = await SinglesAsync(OneSetOf6);
        (await UsualWeekAsync(game.Una, Usual(15))).EnsureSuccessStatusCode();
        (await UsualWeekAsync(game.Vic, Usual(15))).EnsureSuccessStatusCode();
        Assert.Equal([Shared(15), Shared(22), Shared(29)], (await GameAsync(game.Una.Client, game.GameId)).Availability!.Shared);

        // Una isn't free on 22 July, only in the morning on 29 July, and free on 16 July, a day Vic isn't.
        (await SetDaysAsync(game.Una, game.LeagueId, [Own(22, free: false), Own(29, from: "10:00", to: "12:00"), Own(16)])).EnsureSuccessStatusCode();
        Assert.Equal([Shared(15), Shared(29, At(10), At(12))], (await GameAsync(game.Vic.Client, game.GameId)).Availability!.Shared);
        var days = (await FreeDaysOfAsync(game.Una, game.LeagueId)).Days;
        Assert.Equal(new AvailabilityDay(On(7, 22), true, false, null, null), days.Single(day => day.Date == On(7, 22)));
        Assert.Equal(new AvailabilityDay(On(7, 29), true, true, At(10), At(12)), days.Single(day => day.Date == On(7, 29)));
        Assert.Equal(new AvailabilityDay(On(7, 15), false, true, null, null), days.Single(day => day.Date == On(7, 15)));

        // None set: every date follows the usual week again.
        (await SetDaysAsync(game.Una, game.LeagueId, [])).EnsureSuccessStatusCode();
        Assert.Equal([Shared(15), Shared(22), Shared(29)], (await GameAsync(game.Vic.Client, game.GameId)).Availability!.Shared);
        Assert.DoesNotContain((await FreeDaysOfAsync(game.Una, game.LeagueId)).Days, day => day.Own);
    }

    [Fact]
    public async Task A_day_whose_times_dont_overlap_isnt_shared()
    {
        var game = await SinglesAsync(OneSetOf6);
        (await SetDaysAsync(game.Una, game.LeagueId, [Own(20, from: "09:00", to: "12:00"), Own(21, from: "09:00", to: "12:00")])).EnsureSuccessStatusCode();
        // 20 July only touches at noon; 21 July overlaps for an hour.
        (await SetDaysAsync(game.Vic, game.LeagueId, [Own(20, from: "12:00", to: "15:00"), Own(21, from: "11:00", to: "15:00")])).EnsureSuccessStatusCode();

        Assert.Equal([Shared(21, At(11), At(12))], (await GameAsync(game.Una.Client, game.GameId)).Availability!.Shared);
    }

    [Fact]
    public async Task A_doubles_Game_shares_the_time_all_four_are_free()
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

        (await UsualWeekAsync(players[0], Usual(16, "18:00", "22:00"))).EnsureSuccessStatusCode();
        (await SetDaysAsync(players[1], leagueId, [Own(16, from: "19:00", to: "23:00")])).EnsureSuccessStatusCode();
        (await SetDaysAsync(players[2], leagueId, [Own(16)])).EnsureSuccessStatusCode();
        (await UsualWeekAsync(players[3], Usual(16, "17:00", "21:30"))).EnsureSuccessStatusCode();

        // 16 July for all four from 19:00 to 21:30; Ann's and Dan's other weeks don't suit Bob or Cat.
        Assert.Equal([Shared(16, At(19), At(21, 30))], (await GameAsync(players[2].Client, gameId)).Availability!.Shared);
    }

    [Fact]
    public async Task A_Foursomes_Game_shares_the_time_all_four_are_free()
    {
        var (manager, leagueId, players, teams) = await FoursomesAsync(Eight[..4]);
        await StartAsync(manager, leagueId, teams, divisions: [4]);
        var game = PairedGame((await LadderOfAsync(manager, leagueId)).Divisions.Single(), teams, 0, 1);
        (await UsualWeekAsync(players[0], Usual(21), Usual(15, "08:00", "10:00"))).EnsureSuccessStatusCode();
        (await UsualWeekAsync(players[1], Usual(21, "10:00", "16:00"), Usual(15))).EnsureSuccessStatusCode();
        (await SetDaysAsync(players[2], leagueId, [Own(21, from: "14:00", to: "18:00"), Own(15, from: "09:00", to: "11:00")])).EnsureSuccessStatusCode();
        (await SetDaysAsync(players[3], leagueId, [Own(15, from: "10:00", to: "12:00"), Own(21, from: "13:00", to: "17:00")])).EnsureSuccessStatusCode();

        // 15 July: 08–10, any time, 09–11 and 10–12 only touch at 10:00. 21 July: 14:00 to 16:00.
        Assert.Equal([Shared(21, At(14), At(16))], (await GameAsync(players[0].Client, game.Id)).Availability!.Shared);
    }

    [Fact]
    public async Task A_usual_week_is_its_holders_alone_and_a_window_must_make_sense()
    {
        var game = await SinglesAsync(OneSetOf6);

        Assert.Equal(HttpStatusCode.NotFound, (await game.Una.Client.GetAsync($"/players/{game.Vic.PlayerId}/usual-week")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await UsualWeekAsync((game.Una.Client, game.Vic.PlayerId), Usual(15))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await UsualWeekAsync((game.Una.Client, Guid.NewGuid()), Usual(15))).StatusCode);

        async Task<string> UsualRefusedAsync(params object[] days) => Assert.Single((await ErrorsAsync(await UsualWeekAsync(game.Una, days)))["days"]);
        Assert.Equal("Give a window both a from and a to time, or neither for any time.", await UsualRefusedAsync(Usual(15, "18:00")));
        Assert.Equal("A window ends after it starts, on the same day.", await UsualRefusedAsync(Usual(15, "22:00", "18:00")));
        Assert.Equal("A window ends after it starts, on the same day.", await UsualRefusedAsync(Usual(15, "18:00", "18:00")));
        Assert.Equal("Give times in whole minutes, like 18:30.", await UsualRefusedAsync(Usual(15, "18:00:30", "20:00")));
        Assert.Equal("Give each day of the week once.", await UsualRefusedAsync(Usual(15), Usual(22, "18:00", "20:00")));
        Assert.Equal("Pick a day of the week, Monday to Sunday.", await UsualRefusedAsync(new { weekday = 0 }));

        async Task<string> DaysRefusedAsync(params object[] days) => Assert.Single((await ErrorsAsync(await SetDaysAsync(game.Una, game.LeagueId, days)))["days"]);
        Assert.Equal("A day you're not free has no times.", await DaysRefusedAsync(Own(15, free: false, from: "18:00", to: "20:00")));
        Assert.Equal("A window ends after it starts, on the same day.", await DaysRefusedAsync(Own(15, from: "20:00", to: "18:00")));
        Assert.Equal("Set each day once.", await DaysRefusedAsync(Own(15), Own(15, free: false)));
        Assert.Empty((await UsualWeekOfAsync(game.Una)).Days);
    }

    [Fact]
    public async Task An_agreed_Date_takes_a_start_time_that_the_Game_the_ladder_and_Home_show()
    {
        var game = await SinglesAsync(OneSetOf6);

        (await ArrangeAsync(game.Una, game.GameId, Day(7, 20), startTime: "18:30")).EnsureSuccessStatusCode();
        var seen = await GameAsync(game.Vic.Client, game.GameId);
        Assert.Equal((On(7, 20), At(18, 30)), (seen.Date, seen.StartTime));
        Assert.Equal(At(18, 30), (await LadderOfAsync(game.Manager, game.LeagueId)).Divisions.Single().Games.Single().StartTime);
        var home = (await game.Vic.Client.GetFromJsonAsync<GetPlayerGamesResponse>($"/players/{game.Vic.PlayerId}/games"))!;
        Assert.Equal(At(18, 30), home.Leagues.Single().Games.Single().StartTime);

        // A Date sent without a time has none, as a Game arranged before times; clearing the Date clears both.
        (await ArrangeAsync(game.Vic, game.GameId, Day(7, 21))).EnsureSuccessStatusCode();
        var untimed = await GameAsync(game.Una.Client, game.GameId);
        Assert.Equal((On(7, 21), (TimeOnly?)null), (untimed.Date, untimed.StartTime));
        (await ArrangeAsync(game.Vic, game.GameId, Day(7, 21), startTime: "19:00")).EnsureSuccessStatusCode();
        (await ArrangeAsync(game.Vic, game.GameId, null)).EnsureSuccessStatusCode();
        var cleared = await GameAsync(game.Una.Client, game.GameId);
        Assert.Equal(((DateOnly?)null, (TimeOnly?)null), (cleared.Date, cleared.StartTime));

        // A time needs a day, in whole minutes.
        Assert.Equal(["Pick the day as well as the time."], (await ErrorsAsync(await ArrangeAsync(game.Una, game.GameId, null, startTime: "18:30")))["startTime"]);
        Assert.Equal(["Give times in whole minutes, like 18:30."], (await ErrorsAsync(await ArrangeAsync(game.Una, game.GameId, Day(7, 20), startTime: "18:30:15")))["startTime"]);
    }

    [Fact]
    public async Task Deleting_an_account_deletes_its_usual_week()
    {
        var game = await SinglesAsync(OneSetOf6);
        (await UsualWeekAsync(game.Una, Usual(20))).EnsureSuccessStatusCode();
        (await UsualWeekAsync(game.Vic, Usual(20))).EnsureSuccessStatusCode();
        Assert.Equal([Shared(13), Shared(20), Shared(27)], (await GameAsync(game.Vic.Client, game.GameId)).Availability!.Shared);

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteUserAsync(await UserIdAsync(game.Una.Client))).StatusCode);

        var availability = (await GameAsync(game.Vic.Client, game.GameId)).Availability!;
        Assert.Empty(availability.Shared);
        Assert.Equal([Player.FormerPlayerName], availability.NotGiven);
    }
}
