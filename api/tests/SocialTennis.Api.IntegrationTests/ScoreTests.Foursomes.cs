using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// Foursomes (#14, ADR-0036): Divisions of 4 Players, each with 3 Games of
/// rotating pairs, ranked by the games each Player banked, and individual
/// Relegation at the close.
/// </summary>
public partial class ScoreTests
{
    private static readonly string[] Eight = ["Ann", "Bob", "Cat", "Dan", "Eve", "Fay", "Gus", "Hal"];

    /// <summary>A Foursomes League of <paramref name="names"/>, each with their Team of one, in the order they joined.</summary>
    private async Task<(HttpClient Manager, Guid LeagueId, List<(HttpClient Client, Guid PlayerId)> Players, Guid[] Teams)> FoursomesAsync(
        string[] names, string stageRoll = "Manual")
    {
        var (manager, clubId) = await ClubAsync();
        var leagueId = await LeagueAsync(manager, clubId, OneSetOf6, type: "Foursomes", stageRoll: stageRoll);
        var players = new List<(HttpClient Client, Guid PlayerId)>();
        foreach (var name in names)
        {
            players.Add(await ParticipantAsync(manager, clubId, leagueId, name));
        }
        var league = await ManagedAsync(manager, leagueId);
        return (manager, leagueId, players, [.. players.Select(p => TeamOf(league, p.PlayerId))]);
    }

    /// <summary>A Game's Side as Team ids: one Team, or a Foursomes pair.</summary>
    private static Guid[] SideOf(Guid team, Guid? partner) => partner is { } other ? [team, other] : [team];

    /// <summary>The Game whose Side 1 is these two places' Players, of the Division's four.</summary>
    private static LadderGame PairedGame(LadderDivision division, Guid[] teams, int first, int partner) =>
        division.Games.Single(g => SideOf(g.FirstTeamId, g.FirstPartnerTeamId).ToHashSet().SetEquals([teams[first], teams[partner]]));

    [Fact]
    public async Task A_Foursomes_ladder_starts_in_Divisions_of_exactly_4_Players()
    {
        var (manager, leagueId, _, teams) = await FoursomesAsync([.. Eight, "Ivy"]);

        // Nine Players, each with a Team of one: two Divisions of 4 fit, and one must be left out.
        var proposal = (await manager.GetFromJsonAsync<GetLeagueStartResponse>($"/leagues/{leagueId}/start"))!;
        Assert.Equal(9, proposal.Competitors.Count);
        Assert.Equal(4, proposal.DivisionSize);
        Assert.Equal([4, 4], proposal.Divisions);
        // The proposal covers the top 8 only: confirmed with Ivy still on the ladder, it doesn't add up. The Manager chooses whom to leave out.
        var asProposed = await manager.PostAsJsonAsync(
            $"/leagues/{leagueId}/start", new { ladder = teams, divisions = proposal.Divisions, leftOut = Array.Empty<Guid>(), removed = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.BadRequest, asProposed.StatusCode);

        foreach (var divisions in new[] { new[] { 5, 4 }, new[] { 3, 3, 3 }, new[] { 9 } })
        {
            var refused = await manager.PostAsJsonAsync(
                $"/leagues/{leagueId}/start", new { ladder = teams, divisions, leftOut = Array.Empty<Guid>(), removed = Array.Empty<Guid>() });
            Assert.Equal(["A Foursomes Division is always 4 Players: add Players, or leave some out, to make a multiple of 4."], (await ErrorsAsync(refused))["divisions"]);
        }

        await StartAsync(manager, leagueId, teams[..8], leftOut: [teams[8]], divisions: [4, 4]);
        var ladder = await LadderOfAsync(manager, leagueId);
        Assert.Equal([[.. teams[..4]], [.. teams[4..8]]], ladder.Divisions.Select(d => d.Competitors.Select(c => c.TeamId).ToArray()));
        // Each Division's 3 Games, in rotation order: 1+2 v 3+4, 1+3 v 2+4, 1+4 v 2+3.
        Assert.Equal(
            [
                (teams[0], teams[1], teams[2], teams[3]),
                (teams[0], teams[2], teams[1], teams[3]),
                (teams[0], teams[3], teams[1], teams[2]),
            ],
            ladder.Divisions[0].Games.Select(g => (g.FirstTeamId, g.FirstPartnerTeamId!.Value, g.SecondTeamId, g.SecondPartnerTeamId!.Value)));
        Assert.Equal(3, ladder.Divisions[1].Games.Count);
    }

    [Fact]
    public async Task Any_of_the_four_enters_a_Score_and_each_Player_banks_their_Sides_games()
    {
        var (manager, leagueId, players, teams) = await FoursomesAsync(Eight[..4]);
        await StartAsync(manager, leagueId, teams, divisions: [4]);
        var division = (await LadderOfAsync(manager, leagueId)).Divisions.Single();
        var (abVcd, acVbd, adVbc) = (PairedGame(division, teams, 0, 1), PairedGame(division, teams, 0, 2), PairedGame(division, teams, 0, 3));

        // The Match screen has both pairs. Dan, fourth, is never first on a Side, and enters every Score.
        var game = await GameAsync(players[3].Client, adVbc.Id);
        Assert.Equal(
            [["Ann", "Dan"], ["Bob", "Cat"]],
            game.Sides.Select(s => s.Players.Select(p => p.Name.Split(' ')[0]).ToArray()));
        Assert.Equal([players[3].PlayerId], game.YourPlayerIds);
        (await SubmitAsync(players[3].Client, abVcd.Id, players[3].PlayerId, [Set(6, 2)])).EnsureSuccessStatusCode();
        (await SubmitAsync(players[3].Client, acVbd.Id, players[3].PlayerId, [Set(3, 6)])).EnsureSuccessStatusCode();
        (await SubmitAsync(players[3].Client, adVbc.Id, players[3].PlayerId, [Set(6, 4)])).EnsureSuccessStatusCode();

        // Ann 6+3+6 = 15, Bob 6+6+4 = 16, Cat 2+3+4 = 9, Dan 2+6+6 = 14.
        var standings = (await LadderOfAsync(players[0].Client, leagueId)).Divisions.Single().Standings;
        Assert.Equal(
            [(teams[1], 1, 16, 2), (teams[0], 2, 15, 2), (teams[3], 3, 14, 2), (teams[2], 4, 9, 0)],
            standings.Select(s => (s.TeamId, (int)s.Rank, (int)s.GamesFor, (int)s.Won)));
        Assert.All(standings, s => Assert.Null(s.Points));

        // Dan's own Games, from his Side: his partner, and the pair against him.
        var dans = (await players[3].Client.GetFromJsonAsync<GetPlayerGamesResponse>($"/players/{players[3].PlayerId}/games"))!.Leagues.Single().Games;
        Assert.Equal(
            [(2, "Cat", "Ann & Bob"), (2, "Bob", "Ann & Cat"), (1, "Ann", "Bob & Cat")],
            dans.Select(g => ((int)g.YourSide, g.Partners.Single().Split(' ')[0], string.Join(" & ", g.Opponents.Select(o => o.Split(' ')[0])))));
    }

    [Fact]
    public async Task At_the_close_each_Divisions_top_Player_goes_up_and_bottom_Player_down()
    {
        var (manager, leagueId, players, teams) = await FoursomesAsync(Eight);
        await StartAsync(manager, leagueId, teams, divisions: [4, 4]);
        var ladder = await LadderOfAsync(manager, leagueId);
        // In each Division 1+2 v 3+4 and 1+3 v 2+4 go 6–2 to the first place's pair, and 1+4 v 2+3 goes 6–4 to 2+3:
        // place 1 banks 6+6+4 = 16, places 2 and 3 6+2+6 = 14, place 4 2+2+4 = 8.
        foreach (var (division, offset) in ladder.Divisions.Select((d, index) => (d, index * 4)))
        {
            var top = players[offset];
            (await SubmitAsync(top.Client, PairedGame(division, teams, offset, offset + 1).Id, top.PlayerId, [Set(6, 2)])).EnsureSuccessStatusCode();
            (await SubmitAsync(top.Client, PairedGame(division, teams, offset, offset + 2).Id, top.PlayerId, [Set(6, 2)])).EnsureSuccessStatusCode();
            (await SubmitAsync(top.Client, PairedGame(division, teams, offset, offset + 3).Id, top.PlayerId, [Set(4, 6)])).EnsureSuccessStatusCode();
        }

        var screen = await CloseScreenAsync(manager, leagueId);
        Assert.Empty(screen.Waiting);
        Assert.Equal([16, 14, 14, 8], screen.Divisions[0].Standings.Select(s => (int)s.GamesFor));
        // Bob and Cat are level in second, which moves nobody: no tie to order.
        Assert.Empty(screen.Awaiting.Ties);
        // Nobody joins a Foursomes ladder at the close yet: none is offered, and naming one is refused.
        Assert.Equal(
            "league-changed",
            await ProblemTypeAsync(
                await CloseAsync(manager, leagueId, Order([.. teams[..4]], [.. teams[4..]]), joining: [teams[0]], start: $"{Year}-08-01", end: $"{Year}-08-31"),
                HttpStatusCode.Conflict));
        (await CloseAsync(manager, leagueId, Order([.. teams[..4]], [.. teams[4..]]), start: $"{Year}-08-01", end: $"{Year}-08-31"))
            .EnsureSuccessStatusCode();

        // Dan goes down to the top of Division 2, Eve up to its bottom; Bob and Cat, Fay and Gus stay.
        var next = await LadderOfAsync(manager, leagueId);
        Assert.Equal(
            [[teams[0], teams[1], teams[2], teams[4]], [teams[3], teams[5], teams[6], teams[7]]],
            next.Divisions.Select(d => d.Competitors.Select(c => c.TeamId).ToArray()));
        // Stage 2 has its own rotation of the new four: Ann partners Eve.
        Assert.Contains(next.Divisions[0].Games, g => g.FirstTeamId == teams[0] && g.FirstPartnerTeamId == teams[4]);
        Assert.All(next.Divisions, d => Assert.Equal(3, d.Games.Count));
    }

    [Fact]
    public async Task Players_level_on_games_at_a_place_that_moves_are_a_tie_for_the_Manager()
    {
        var (manager, leagueId, players, teams) = await FoursomesAsync(Eight);
        await StartAsync(manager, leagueId, teams, divisions: [4, 4]);
        var ladder = await LadderOfAsync(manager, leagueId);
        // Division 1: 6–4 three times for the first place's pair: Ann 18, then Bob, Cat and Dan 6+4+4 = 14 each, level for last.
        var top = ladder.Divisions[0];
        foreach (var partner in new[] { 1, 2, 3 })
        {
            (await SubmitAsync(players[0].Client, PairedGame(top, teams, 0, partner).Id, players[0].PlayerId, [Set(6, 4)])).EnsureSuccessStatusCode();
        }
        // Division 2: a Game not played leaves its moves to the Manager: all four are one tie.
        var bottom = ladder.Divisions[1];
        (await SubmitAsync(players[4].Client, PairedGame(bottom, teams, 4, 5).Id, players[4].PlayerId, [Set(6, 1)])).EnsureSuccessStatusCode();
        (await SubmitAsync(players[4].Client, PairedGame(bottom, teams, 4, 6).Id, players[4].PlayerId, [Set(6, 1)])).EnsureSuccessStatusCode();
        (await VoidAsync(manager, PairedGame(bottom, teams, 4, 7).Id)).EnsureSuccessStatusCode();

        var screen = await CloseScreenAsync(manager, leagueId);
        Assert.Equal(
            [[teams[1], teams[2], teams[3]], [teams[4], teams[5], teams[6], teams[7]]],
            screen.Awaiting.Ties.Select(t => t.TeamIds.ToArray()));
        // The Manager orders them: Cat goes down, and Hal comes up from Division 2.
        (await CloseAsync(manager, leagueId, Order([teams[0], teams[1], teams[3], teams[2]], [teams[7], teams[4], teams[5], teams[6]]), start: $"{Year}-08-01", end: $"{Year}-08-31"))
            .EnsureSuccessStatusCode();
        Assert.Equal(
            [[teams[0], teams[1], teams[3], teams[7]], [teams[2], teams[4], teams[5], teams[6]]],
            (await LadderOfAsync(manager, leagueId)).Divisions.Select(d => d.Competitors.Select(c => c.TeamId).ToArray()));
    }

    [Fact]
    public async Task The_auto_roll_job_closes_a_Foursomes_Stage_with_its_rotation()
    {
        var (manager, leagueId, players, teams) = await FoursomesAsync(Eight, stageRoll: "Automatic");
        await StartAsync(manager, leagueId, teams, divisions: [4, 4]);
        var ladder = await LadderOfAsync(manager, leagueId);
        foreach (var (division, offset) in ladder.Divisions.Select((d, index) => (d, index * 4)))
        {
            var top = players[offset];
            (await SubmitAsync(top.Client, PairedGame(division, teams, offset, offset + 1).Id, top.PlayerId, [Set(6, 2)])).EnsureSuccessStatusCode();
            (await SubmitAsync(top.Client, PairedGame(division, teams, offset, offset + 2).Id, top.PlayerId, [Set(6, 2)])).EnsureSuccessStatusCode();
            (await SubmitAsync(top.Client, PairedGame(division, teams, offset, offset + 3).Id, top.PlayerId, [Set(4, 6)])).EnsureSuccessStatusCode();
        }

        factory.Clock.Now = new DateTimeOffset(Year, 7, 31, 23, 5, 0, TimeSpan.Zero);
        await RollAsync();

        var rolled = await LadderOfAsync(manager, leagueId);
        Assert.Equal(2, (int)rolled.Stage!.Number);
        Assert.Equal(
            [[teams[0], teams[1], teams[2], teams[4]], [teams[3], teams[5], teams[6], teams[7]]],
            rolled.Divisions.Select(d => d.Competitors.Select(c => c.TeamId).ToArray()));
    }

    [Fact]
    public async Task A_Foursomes_Player_can_t_move_Division_mid_Stage()
    {
        var (manager, leagueId, _, teams) = await FoursomesAsync(Eight);
        await StartAsync(manager, leagueId, teams, divisions: [4, 4]);

        var moved = await manager.PostAsJsonAsync($"/leagues/{leagueId}/moves", new { teamId = teams[0], toDivision = 2 });

        Assert.Equal("foursomes-fixed", await ProblemTypeAsync(moved, HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task The_migration_gives_each_Foursomes_participant_a_Team_of_one()
    {
        var (manager, leagueId, players, _) = await FoursomesAsync(Eight[..3]);
        using (var scope = factory.Services.CreateScope())
        {
            // As before #14: Foursomes participants had no Team.
            var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
            await db.LeagueParticipants.Where(p => p.LeagueId == leagueId).ExecuteUpdateAsync(set => set.SetProperty(p => p.TeamId, (Guid?)null));
            await db.Teams.Where(t => t.LeagueId == leagueId).ExecuteDeleteAsync();

            foreach (var sql in new SocialTennis.Api.Migrations.FoursomesSides().UpOperations.OfType<SqlOperation>().Select(o => o.Sql))
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        var league = await ManagedAsync(manager, leagueId);
        Assert.Equal(3, league.Teams.Count);
        Assert.All(players, p => Assert.Single(league.Teams, t => t.PlayerIds.SequenceEqual([p.PlayerId])));
    }
}
