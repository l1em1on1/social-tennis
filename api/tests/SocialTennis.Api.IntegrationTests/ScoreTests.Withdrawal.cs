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
    public async Task The_Manager_withdraws_a_Team_directly_and_the_Stage_waits_for_the_close_that_takes_it_off()
    {
        var game = await SinglesAsync(OneSetOf6);
        var league = await ManagedAsync(game.Manager, game.LeagueId);
        var vic = TeamOf(league, game.Vic.PlayerId);

        Assert.Equal(HttpStatusCode.Forbidden, (await game.Una.Client.PutAsync($"/leagues/{game.LeagueId}/teams/{vic}/withdrawal", null)).StatusCode);
        (await game.Manager.PutAsync($"/leagues/{game.LeagueId}/teams/{vic}/withdrawal", null)).EnsureSuccessStatusCode();
        Assert.Equal("already-withdrawn", await ProblemTypeAsync(await game.Manager.PutAsync($"/leagues/{game.LeagueId}/teams/{vic}/withdrawal", null), HttpStatusCode.Conflict));
        // Vic sees what happened, though he never asked.
        var withdrawal = (await MyWithdrawalAsync(game.Vic, game.LeagueId))!;
        Assert.Equal(WithdrawalStatus.Accepted, withdrawal.Status);
        Assert.NotNull(withdrawal.DecidedAt);

        // Its Game is marked not played; the close then waits for part 2 rather than carry Vic into Stage 2.
        (await VoidAsync(game.Manager, game.GameId)).EnsureSuccessStatusCode();
        Assert.Equal(
            "withdrawn-on-ladder",
            await ProblemTypeAsync(
                await CloseAsync(game.Manager, game.LeagueId, Order([TeamOf(league, game.Una.PlayerId), vic]), start: $"{Year}-08-01", end: $"{Year}-08-31"),
                HttpStatusCode.Conflict));
        factory.Clock.Now = new DateTimeOffset(Year, 7, 31, 23, 5, 0, TimeSpan.Zero);
        await RollAsync();
        Assert.Equal(1, (int)(await LadderOfAsync(game.Una.Client, game.LeagueId)).Stage!.Number);
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

        // Closing the last Stage carries nobody into another: the League ends.
        (await CloseAsync(manager, leagueId, Order([teams[0], teams[1]], [teams[2], teams[3]]))).EnsureSuccessStatusCode();
        Assert.True((await LadderOfAsync(manager, leagueId)).Ended);
    }
}
