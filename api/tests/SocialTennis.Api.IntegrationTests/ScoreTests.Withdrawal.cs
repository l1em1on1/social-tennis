using System.Net;
using System.Net.Http.Json;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// Withdrawal (#21, part 1): a Player asks to leave a running League, the
/// Manager decides or withdraws a Team directly, and a withdrawn Team's Games
/// not played don't cost its opponents the bonus point.
/// </summary>
public partial class ScoreTests
{
    private static Task<HttpResponseMessage> AskToWithdrawAsync((HttpClient Client, Guid PlayerId) player, Guid leagueId) =>
        player.Client.PostAsJsonAsync($"/leagues/{leagueId}/withdrawals", new { playerId = player.PlayerId });

    private static Task<HttpResponseMessage> DecideAsync(HttpClient manager, Guid leagueId, Guid requestId, bool accept) =>
        manager.PutAsJsonAsync($"/leagues/{leagueId}/withdrawals/{requestId}", new { accept });

    private static async Task<PlayerLeagueWithdrawal?> MyWithdrawalAsync((HttpClient Client, Guid PlayerId) player, Guid leagueId) =>
        (await player.Client.GetFromJsonAsync<GetPlayerLeaguesResponse>($"/players/{player.PlayerId}/leagues"))!
            .Leagues.Single(l => l.Id == leagueId).Withdrawal;

    [Fact]
    public async Task A_Player_asks_to_withdraw_and_can_cancel_and_the_Manager_declines_or_accepts()
    {
        var game = await SinglesAsync(OneSetOf6);

        (await AskToWithdrawAsync(game.Una, game.LeagueId)).EnsureSuccessStatusCode();
        Assert.Equal("already-asked", await ProblemTypeAsync(await AskToWithdrawAsync(game.Una, game.LeagueId), HttpStatusCode.Conflict));
        var waiting = (await ManagedAsync(game.Manager, game.LeagueId)).WithdrawalRequests.Single();
        Assert.StartsWith("Una", waiting.PlayerName);
        Assert.Equal(WithdrawalStatus.Pending, (await MyWithdrawalAsync(game.Una, game.LeagueId))!.Status);

        // Taken back: it's gone, as if never asked.
        Assert.Equal(HttpStatusCode.NoContent, (await game.Una.Client.DeleteAsync($"/leagues/{game.LeagueId}/players/{game.Una.PlayerId}/withdrawal")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await game.Una.Client.DeleteAsync($"/leagues/{game.LeagueId}/players/{game.Una.PlayerId}/withdrawal")).StatusCode);
        Assert.Empty((await ManagedAsync(game.Manager, game.LeagueId)).WithdrawalRequests);
        Assert.Null(await MyWithdrawalAsync(game.Una, game.LeagueId));

        // Declined: nothing changes, and Una sees it; she may ask again, and is accepted.
        (await AskToWithdrawAsync(game.Una, game.LeagueId)).EnsureSuccessStatusCode();
        var first = (await ManagedAsync(game.Manager, game.LeagueId)).WithdrawalRequests.Single();
        Assert.Equal(HttpStatusCode.Forbidden, (await DecideAsync(game.Una.Client, game.LeagueId, first.Id, true)).StatusCode);
        // A decision left out is no decision: a 400, and the request still waits.
        var missing = await game.Manager.PutAsJsonAsync($"/leagues/{game.LeagueId}/withdrawals/{first.Id}", new { });
        Assert.True((await ErrorsAsync(missing)).ContainsKey("accept"));
        (await DecideAsync(game.Manager, game.LeagueId, first.Id, false)).EnsureSuccessStatusCode();
        Assert.Equal("already-decided", await ProblemTypeAsync(await DecideAsync(game.Manager, game.LeagueId, first.Id, true), HttpStatusCode.Conflict));
        Assert.Equal(WithdrawalStatus.Declined, (await MyWithdrawalAsync(game.Una, game.LeagueId))!.Status);
        Assert.False((await LadderOfAsync(game.Una.Client, game.LeagueId)).Divisions.Single().Competitors.Single(c => c.Players.Any(p => p.PlayerId == game.Una.PlayerId)).Withdrawn);

        (await AskToWithdrawAsync(game.Una, game.LeagueId)).EnsureSuccessStatusCode();
        var second = (await ManagedAsync(game.Manager, game.LeagueId)).WithdrawalRequests.Single();
        (await DecideAsync(game.Manager, game.LeagueId, second.Id, true)).EnsureSuccessStatusCode();

        // Withdrawn: still in place, and whole, until the close; she can't ask again.
        var league = await ManagedAsync(game.Manager, game.LeagueId);
        Assert.True(league.Teams.Single(t => t.Id == TeamOf(league, game.Una.PlayerId)).Withdrawn);
        Assert.True((await LadderOfAsync(game.Vic.Client, game.LeagueId)).Divisions.Single().Competitors.Single(c => c.Players.Any(p => p.PlayerId == game.Una.PlayerId)).Withdrawn);
        Assert.Equal(WithdrawalStatus.Accepted, (await MyWithdrawalAsync(game.Una, game.LeagueId))!.Status);
        Assert.Equal("already-withdrawn", await ProblemTypeAsync(await AskToWithdrawAsync(game.Una, game.LeagueId), HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Only_a_Team_on_the_running_ladder_withdraws()
    {
        var game = await SinglesAsync(OneSetOf6);

        // Wes was left out at the start: the Manager removes him instead.
        Assert.Equal("not-on-the-ladder", await ProblemTypeAsync(await AskToWithdrawAsync(game.Wes, game.LeagueId), HttpStatusCode.Conflict));
        // Someone else's Player, or a League they're not in: as if it weren't there.
        Assert.Equal(HttpStatusCode.NotFound, (await game.Vic.Client.PostAsJsonAsync($"/leagues/{game.LeagueId}/withdrawals", new { playerId = game.Una.PlayerId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AskToWithdrawAsync(game.Una, Guid.NewGuid())).StatusCode);
        var league = await ManagedAsync(game.Manager, game.LeagueId);
        Assert.Equal("not-on-the-ladder", await ProblemTypeAsync(await game.Manager.PutAsync($"/leagues/{game.LeagueId}/teams/{TeamOf(league, game.Wes.PlayerId)}/withdrawal", null), HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task The_Manager_withdraws_a_Team_directly_and_a_replacement_takes_its_place_at_the_close()
    {
        var game = await SinglesAsync(OneSetOf6);
        var league = await ManagedAsync(game.Manager, game.LeagueId);
        var (una, vic, wes) = (TeamOf(league, game.Una.PlayerId), TeamOf(league, game.Vic.PlayerId), TeamOf(league, game.Wes.PlayerId));

        Assert.Equal(HttpStatusCode.Forbidden, (await game.Una.Client.PutAsync($"/leagues/{game.LeagueId}/teams/{vic}/withdrawal", null)).StatusCode);
        (await game.Manager.PutAsync($"/leagues/{game.LeagueId}/teams/{vic}/withdrawal", null)).EnsureSuccessStatusCode();
        Assert.Equal("already-withdrawn", await ProblemTypeAsync(await game.Manager.PutAsync($"/leagues/{game.LeagueId}/teams/{vic}/withdrawal", null), HttpStatusCode.Conflict));
        // Vic sees what happened, though he never asked.
        var withdrawal = (await MyWithdrawalAsync(game.Vic, game.LeagueId))!;
        Assert.Equal(WithdrawalStatus.Accepted, withdrawal.Status);
        Assert.NotNull(withdrawal.DecidedAt);
        (await VoidAsync(game.Manager, game.GameId)).EnsureSuccessStatusCode();

        // Without a replacement Una would be alone in her Division: the close says so, and auto-roll holds.
        var alone = await CloseAsync(game.Manager, game.LeagueId, Order([una, vic]), start: $"{Year}-08-01", end: $"{Year}-08-31");
        Assert.Equal(["Division 1 would be left with one Team: name a replacement for a Team that has withdrawn."], (await ErrorsAsync(alone))["replacements"]);
        Assert.Equal([wes], (await CloseScreenAsync(game.Manager, game.LeagueId)).Replaceable.Select(t => t.TeamId));

        // Wes, waiting off the ladder, takes Vic's place.
        (await CloseWithAsync(game.Manager, game.LeagueId, Order([una, vic]), [new(vic, wes)])).EnsureSuccessStatusCode();
        var next = await LadderOfAsync(game.Una.Client, game.LeagueId);
        Assert.Equal([una, wes], next.Divisions.Single().Competitors.Select(c => c.TeamId));
    }

    [Fact]
    public async Task A_Division_every_Team_has_withdrawn_from_says_it_would_be_left_with_none()
    {
        var game = await SinglesAsync(OneSetOf6);
        var league = await ManagedAsync(game.Manager, game.LeagueId);
        var (una, vic) = (TeamOf(league, game.Una.PlayerId), TeamOf(league, game.Vic.PlayerId));
        (await game.Manager.PutAsync($"/leagues/{game.LeagueId}/teams/{una}/withdrawal", null)).EnsureSuccessStatusCode();
        (await game.Manager.PutAsync($"/leagues/{game.LeagueId}/teams/{vic}/withdrawal", null)).EnsureSuccessStatusCode();
        (await VoidAsync(game.Manager, game.GameId)).EnsureSuccessStatusCode();

        var empty = await CloseAsync(game.Manager, game.LeagueId, Order([una, vic]), start: $"{Year}-08-01", end: $"{Year}-08-31");
        Assert.Equal(["Division 1 would be left with no Teams: name replacements for the Teams that have withdrawn."], (await ErrorsAsync(empty))["replacements"]);
    }

    private static Task<HttpResponseMessage> CloseWithAsync(HttpClient manager, Guid leagueId, Guid[][] divisions, CloseReplacement[] replacements, Guid[]? joining = null) =>
        manager.PostAsJsonAsync(
            $"/leagues/{leagueId}/close",
            new { divisions, joining = joining ?? [], nextStartDate = $"{Year}-08-01", nextEndDate = $"{Year}-08-31", replacements });

    /// <summary>
    /// Six singles Players in two Divisions of 3, all played out (Ann, Bob, Cat; then Dan, Eve, Fay, each in that order),
    /// Cat withdrawn, and Gus left out at the start, waiting.
    /// </summary>
    private async Task<(HttpClient Manager, Guid LeagueId, Guid[] Teams)> CatWithdrawnAsync(string stageRoll = "Manual")
    {
        var (manager, clubId) = await ClubAsync();
        var leagueId = await LeagueAsync(manager, clubId, OneSetOf6, stageRoll: stageRoll);
        var players = new List<(HttpClient Client, Guid PlayerId)>();
        foreach (var name in new[] { "Ann", "Bob", "Cat", "Dan", "Eve", "Fay", "Gus" })
        {
            players.Add(await ParticipantAsync(manager, clubId, leagueId, name));
        }
        var league = await ManagedAsync(manager, leagueId);
        var teams = players.Select(p => TeamOf(league, p.PlayerId)).ToArray();
        await StartAsync(manager, leagueId, teams[..6], leftOut: [teams[6]], divisions: [3, 3]);
        var games = (await LadderOfAsync(manager, leagueId)).Divisions.SelectMany(d => d.Games).ToList();
        foreach (var (winner, loser) in new[] { (0, 1), (0, 2), (1, 2), (3, 4), (3, 5), (4, 5) })
        {
            var game = games.Single(g => new[] { g.FirstTeamId, g.SecondTeamId }.ToHashSet().SetEquals([teams[winner], teams[loser]]));
            var set = game.FirstTeamId == teams[winner] ? Set(6, 2) : Set(2, 6);
            (await SubmitAsync(players[winner].Client, game.Id, players[winner].PlayerId, [set])).EnsureSuccessStatusCode();
        }
        (await manager.PutAsync($"/leagues/{leagueId}/teams/{teams[2]}/withdrawal", null)).EnsureSuccessStatusCode();
        return (manager, leagueId, teams);
    }

    [Fact]
    public async Task At_the_close_a_withdrawn_Team_leaves_the_ladder_and_moves_nobody_else()
    {
        var (manager, leagueId, teams) = await CatWithdrawnAsync();

        // Cat, last in Division 1, would have gone down: leaving, she does, and Bob stays. Dan comes up.
        (await CloseAsync(manager, leagueId, Order([.. teams[..3]], [.. teams[3..6]]), start: $"{Year}-08-01", end: $"{Year}-08-31")).EnsureSuccessStatusCode();

        Assert.Equal(
            [[teams[0], teams[1], teams[3]], [teams[4], teams[5]]],
            (await LadderOfAsync(manager, leagueId)).Divisions.Select(d => d.Competitors.Select(c => c.TeamId).ToArray()));
    }

    [Fact]
    public async Task A_replacement_takes_the_place_the_withdrawn_Team_would_have_moved_to()
    {
        var (manager, leagueId, teams) = await CatWithdrawnAsync();
        var order = Order([.. teams[..3]], [.. teams[3..6]]);

        // Only a withdrawn Team is replaced, and only by a Team off the ladder.
        Assert.True((await ErrorsAsync(await CloseWithAsync(manager, leagueId, order, [new(teams[1], teams[6])]))).ContainsKey("replacements"));
        Assert.Equal("league-changed", await ProblemTypeAsync(await CloseWithAsync(manager, leagueId, order, [new(teams[2], teams[4])]), HttpStatusCode.Conflict));
        // Cat named twice, or a replacement that isn't there, is a malformed request, not a server error (#272 review).
        Assert.True((await ErrorsAsync(await CloseWithAsync(manager, leagueId, order, [new(teams[2], teams[6]), new(teams[2], teams[5])]))).ContainsKey("replacements"));
        var holed = await manager.PostAsJsonAsync(
            $"/leagues/{leagueId}/close",
            new { divisions = order, joining = Array.Empty<Guid>(), nextStartDate = $"{Year}-08-01", nextEndDate = $"{Year}-08-31", replacements = new CloseReplacement?[] { null } });
        Assert.True((await ErrorsAsync(holed)).ContainsKey("replacements"));
        // Gus can't both join and replace Cat: he would hold two places in the next Stage (#272 review).
        Assert.Equal("league-changed", await ProblemTypeAsync(await CloseWithAsync(manager, leagueId, order, [new(teams[2], teams[6])], joining: [teams[6]]), HttpStatusCode.Conflict));

        // Gus takes the place Cat goes down to: the top of Division 2.
        (await CloseWithAsync(manager, leagueId, order, [new(teams[2], teams[6])])).EnsureSuccessStatusCode();
        Assert.Equal(
            [[teams[0], teams[1], teams[3]], [teams[6], teams[4], teams[5]]],
            (await LadderOfAsync(manager, leagueId)).Divisions.Select(d => d.Competitors.Select(c => c.TeamId).ToArray()));
    }

    [Fact]
    public async Task The_auto_roll_job_closes_with_a_withdrawn_Team_leaving_the_ladder()
    {
        var (manager, leagueId, teams) = await CatWithdrawnAsync("Automatic");

        factory.Clock.Now = new DateTimeOffset(Year, 7, 31, 23, 5, 0, TimeSpan.Zero);
        await RollAsync();

        Assert.Equal(
            [[teams[0], teams[1], teams[3]], [teams[4], teams[5]]],
            (await LadderOfAsync(manager, leagueId)).Divisions.Select(d => d.Competitors.Select(c => c.TeamId).ToArray()));
    }

    [Fact]
    public async Task A_Foursomes_Player_who_withdraws_must_be_replaced_at_the_close()
    {
        var (manager, leagueId, players, teams) = await FoursomesAsync([.. Eight, "Ivy"]);
        await StartAsync(manager, leagueId, teams[..8], leftOut: [teams[8]], divisions: [4, 4]);
        var ladder = await LadderOfAsync(manager, leagueId);
        foreach (var (division, offset) in ladder.Divisions.Select((d, index) => (d, index * 4)))
        {
            var top = players[offset];
            (await SubmitAsync(top.Client, PairedGame(division, teams, offset, offset + 1).Id, top.PlayerId, [Set(6, 2)])).EnsureSuccessStatusCode();
            (await SubmitAsync(top.Client, PairedGame(division, teams, offset, offset + 2).Id, top.PlayerId, [Set(6, 2)])).EnsureSuccessStatusCode();
            (await SubmitAsync(top.Client, PairedGame(division, teams, offset, offset + 3).Id, top.PlayerId, [Set(4, 6)])).EnsureSuccessStatusCode();
        }
        (await manager.PutAsync($"/leagues/{leagueId}/teams/{teams[3]}/withdrawal", null)).EnsureSuccessStatusCode();
        var order = Order([.. teams[..4]], [.. teams[4..8]]);

        Assert.Equal(
            ["A Foursomes Division is always 4: name a replacement for each Player who has withdrawn."],
            (await ErrorsAsync(await CloseAsync(manager, leagueId, order, start: $"{Year}-08-01", end: $"{Year}-08-31")))["replacements"]);
        Assert.Equal([teams[8]], (await CloseScreenAsync(manager, leagueId)).Replaceable.Select(t => t.TeamId));

        // Ivy takes the place Dan goes down to, and Division 2 plays its rotation with her.
        (await CloseWithAsync(manager, leagueId, order, [new(teams[3], teams[8])])).EnsureSuccessStatusCode();
        var next = await LadderOfAsync(manager, leagueId);
        Assert.Equal(
            [[teams[0], teams[1], teams[2], teams[4]], [teams[8], teams[5], teams[6], teams[7]]],
            next.Divisions.Select(d => d.Competitors.Select(c => c.TeamId).ToArray()));
        Assert.All(next.Divisions, d => Assert.Equal(3, d.Games.Count));
    }

    [Fact]
    public async Task A_Game_not_played_against_a_withdrawn_Team_doesnt_cost_its_opponent_the_bonus_point()
    {
        var (manager, clubId) = await ClubAsync();
        var leagueId = await LeagueAsync(manager, clubId, OneSetOf6, standingsMode: "BonusPoints");
        var una = await ParticipantAsync(manager, clubId, leagueId, "Una");
        var vic = await ParticipantAsync(manager, clubId, leagueId, "Vic");
        var wes = await ParticipantAsync(manager, clubId, leagueId, "Wes");
        var league = await ManagedAsync(manager, leagueId);
        var (u, v, w) = (TeamOf(league, una.PlayerId), TeamOf(league, vic.PlayerId), TeamOf(league, wes.PlayerId));
        await StartAsync(manager, leagueId, [u, v, w]);
        var games = (await LadderOfAsync(manager, leagueId)).Divisions.Single().Games;
        LadderGame Between(Guid one, Guid other) => games.Single(g => new[] { g.FirstTeamId, g.SecondTeamId }.ToHashSet().SetEquals([one, other]));
        (await SubmitAsync(una.Client, Between(u, v).Id, una.PlayerId, [Set(6, 2)])).EnsureSuccessStatusCode();
        (await VoidAsync(manager, Between(u, w).Id)).EnsureSuccessStatusCode();
        int? Of(GetLeagueLadderResponse ladder, Guid team) => ladder.Divisions.Single().Standings.Single(s => s.TeamId == team).Points;

        // Before Wes withdraws, the Game against him not played keeps Una from the bonus: 1 won + 1 played.
        Assert.Equal(2, Of(await LadderOfAsync(manager, leagueId), u));

        (await manager.PutAsync($"/leagues/{leagueId}/teams/{w}/withdrawal", null)).EnsureSuccessStatusCode();

        // Withdrawn: it no longer counts against Una, who has played every Game left: 1 + 1 + 1. Vic still has
        // Vic v Wes to settle, so no bonus for him yet: 0 + 1.
        var ladder = await LadderOfAsync(manager, leagueId);
        Assert.Equal(3, Of(ladder, u));
        Assert.Equal(1, Of(ladder, v));
    }

    [Fact]
    public async Task Deleting_the_account_of_a_Manager_who_decided_a_withdrawal_keeps_the_decision()
    {
        var game = await SinglesAsync(OneSetOf6);
        (await AskToWithdrawAsync(game.Una, game.LeagueId)).EnsureSuccessStatusCode();
        var request = (await ManagedAsync(game.Manager, game.LeagueId)).WithdrawalRequests.Single();
        (await DecideAsync(game.Manager, game.LeagueId, request.Id, false)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteUserAsync(await UserIdAsync(game.Manager))).StatusCode);

        Assert.Equal(WithdrawalStatus.Declined, (await MyWithdrawalAsync(game.Una, game.LeagueId))!.Status);
    }

    [Fact]
    public async Task The_Leagues_last_Stage_closes_with_a_withdrawn_Team_on_it()
    {
        var (manager, leagueId, players, teams) = await TwoDivisionsAsync(endDate: $"{Year}-07-31");
        var ladder = await LadderOfAsync(manager, leagueId);
        (await manager.PutAsync($"/leagues/{leagueId}/teams/{teams[1]}/withdrawal", null)).EnsureSuccessStatusCode();
        (await VoidAsync(manager, ladder.Divisions[0].Games.Single().Id)).EnsureSuccessStatusCode();
        (await SubmitAsync(players[2].Client, ladder.Divisions[1].Games.Single().Id, players[2].PlayerId, [Set(6, 4), Set(6, 4)])).EnsureSuccessStatusCode();

        // Cal asks to withdraw too, and the Manager closes before deciding.
        (await AskToWithdrawAsync(players[2], leagueId)).EnsureSuccessStatusCode();

        // Closing the last Stage carries nobody into another: the League ends, and Cal's request with it.
        (await CloseAsync(manager, leagueId, Order([teams[0], teams[1]], [teams[2], teams[3]]))).EnsureSuccessStatusCode();
        Assert.True((await LadderOfAsync(manager, leagueId)).Ended);
        Assert.Empty((await ManagedAsync(manager, leagueId)).WithdrawalRequests);
        Assert.Null(await MyWithdrawalAsync(players[2], leagueId));
    }
}
