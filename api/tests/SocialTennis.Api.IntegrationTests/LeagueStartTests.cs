using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Features.Leagues.Contracts;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// Issue #7, part 1: a Manager starts a Divisional League from a proposed
/// ladder they may reorder and cut, which opens Stage 1 with a Game per
/// Division and a Bye at the bottom; and what the ladder fixes afterwards.
/// </summary>
public class LeagueStartTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
{
    private static object ALeague(string type, string structure = "Divisional") => new
    {
        name = $"{type} {structure} League",
        type,
        structure,
        startDate = "2026-12-01",
        endDate = "2027-03-31",
        stageLength = new { count = 1, unit = "Months" },
        stageRoll = structure == "Divisional" ? "Manual" : null,
        scoreFormat = new { gamesPerSet = 6, setsPerGame = 1, matchTiebreakDecider = false },
    };

    private async Task<Guid> NewClubAsync()
    {
        var admin = await factory.AdminClientAsync();
        var created = await admin.PostAsJsonAsync("/clubs", new { name = $"Ladder Club {Guid.NewGuid():N}" });
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<CreateClubResponse>())!.Id;
    }

    private static async Task<Guid> CreateLeagueAsync(HttpClient manager, Guid clubId, string type = "SinglesMixed", string structure = "Divisional")
    {
        var response = await manager.PostAsJsonAsync($"/clubs/{clubId}/leagues", ALeague(type, structure));
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

    /// <summary>A Club member the Manager puts straight into the League, optionally with a Rating.</summary>
    private async Task<(HttpClient Client, Guid PlayerId)> ParticipantAsync(
        HttpClient manager, Guid clubId, Guid leagueId, string name, int? rating = null)
    {
        var member = await MemberAsync(manager, clubId, name);
        if (rating is { } value)
        {
            (await manager.PutAsJsonAsync($"/players/{member.PlayerId}/rating", new { rating = value })).EnsureSuccessStatusCode();
        }
        (await manager.PutAsync($"/leagues/{leagueId}/participants/{member.PlayerId}", null)).EnsureSuccessStatusCode();
        return member;
    }

    private static async Task<GetLeagueResponse> LeagueAsync(HttpClient manager, Guid leagueId) =>
        (await manager.GetFromJsonAsync<GetLeagueResponse>($"/leagues/{leagueId}"))!;

    private static async Task<GetLeagueStartResponse> ProposalAsync(HttpClient manager, Guid leagueId) =>
        (await manager.GetFromJsonAsync<GetLeagueStartResponse>($"/leagues/{leagueId}/start"))!;

    private static Task<HttpResponseMessage> StartAsync(
        HttpClient manager, Guid leagueId, IEnumerable<Guid> ladder, IEnumerable<Guid>? leftOut = null, IEnumerable<Guid>? removed = null) =>
        manager.PostAsJsonAsync($"/leagues/{leagueId}/start", new { ladder, leftOut = leftOut ?? [], removed = removed ?? [] });

    /// <summary>The Team of one of a singles participant.</summary>
    private static Guid TeamOf(GetLeagueResponse league, Guid playerId) =>
        league.Teams.Single(t => t.PlayerIds.Contains(playerId)).Id;

    private static async Task<string?> ConflictTypeAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Type;
    }

    /// <summary>Stage 1's Games, as (Division, Side 1's Team, Side 2's Team), straight from the database.</summary>
    private async Task<List<(int Division, Guid First, Guid Second)>> GamesAsync(Guid leagueId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
        var games = await db.Games
            .Where(g => g.LeagueId == leagueId)
            .OrderBy(g => g.Division)
            .Select(g => new { g.Division, Sides = g.Sides.OrderBy(s => s.Side).Select(s => s.TeamId).ToList() })
            .ToListAsync();
        return [.. games.Select(g => (g.Division, g.Sides[0], g.Sides[1]))];
    }

    [Fact]
    public async Task The_proposal_is_by_Rating_then_earliest_join_and_starts_nothing()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (_, early) = await ParticipantAsync(manager, clubId, leagueId, "Early", rating: 3);
        var (_, best) = await ParticipantAsync(manager, clubId, leagueId, "Best", rating: 5);
        var (_, late) = await ParticipantAsync(manager, clubId, leagueId, "Late", rating: 3);
        var (_, lowest) = await ParticipantAsync(manager, clubId, leagueId, "Lowest");

        var proposal = await ProposalAsync(manager, leagueId);

        Assert.Equal([best, early, late, lowest], proposal.Competitors.Select(c => Assert.Single(c.Players).PlayerId));
        Assert.Equal([5.0, 3, 3, 1], proposal.Competitors.Select(c => c.Rating));
        Assert.Empty(proposal.Unpaired);
        Assert.Equal(0, proposal.WaitingRequestCount);
        Assert.Equal(new LeagueStageSummary(1, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), proposal.FirstStage);
        Assert.Null((await LeagueAsync(manager, leagueId)).Stage);
    }

    [Fact]
    public async Task A_doubles_proposal_orders_Teams_by_their_average_and_names_who_would_be_removed()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, "DoublesMix");
        var (_, a) = await ParticipantAsync(manager, clubId, leagueId, "A", rating: 4);
        var (_, b) = await ParticipantAsync(manager, clubId, leagueId, "B", rating: 2);
        var (_, c) = await ParticipantAsync(manager, clubId, leagueId, "C", rating: 5);
        var (_, d) = await ParticipantAsync(manager, clubId, leagueId, "D", rating: 3);
        var (_, alone) = await ParticipantAsync(manager, clubId, leagueId, "Alone");
        (await manager.PostAsJsonAsync($"/leagues/{leagueId}/teams", new { firstPlayerId = a, secondPlayerId = b })).EnsureSuccessStatusCode();
        (await manager.PostAsJsonAsync($"/leagues/{leagueId}/teams", new { firstPlayerId = c, secondPlayerId = d })).EnsureSuccessStatusCode();
        var (asker, askerId) = await MemberAsync(manager, clubId, "Asker");
        (await asker.PostAsJsonAsync($"/leagues/{leagueId}/join-requests", new { playerId = askerId })).EnsureSuccessStatusCode();

        var proposal = await ProposalAsync(manager, leagueId);

        Assert.Equal([4.0, 3], proposal.Competitors.Select(t => t.Rating));
        Assert.Equal(new[] { c, d }.Order(), proposal.Competitors[0].Players.Select(p => p.PlayerId).Order());
        Assert.Equal([alone], proposal.Unpaired.Select(p => p.PlayerId));
        Assert.Equal(1, proposal.WaitingRequestCount);
    }

    [Fact]
    public async Task Starting_places_the_confirmed_order_in_Divisions_of_two_with_one_Game_each()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var ids = new List<Guid>();
        foreach (var name in new[] { "One", "Two", "Three", "Four" })
        {
            ids.Add((await ParticipantAsync(manager, clubId, leagueId, name)).PlayerId);
        }
        var before = await LeagueAsync(manager, leagueId);
        // The Manager reverses the proposal.
        var ladder = ids.AsEnumerable().Reverse().Select(id => TeamOf(before, id)).ToList();

        Assert.Equal(HttpStatusCode.NoContent, (await StartAsync(manager, leagueId, ladder)).StatusCode);

        var league = await LeagueAsync(manager, leagueId);
        Assert.Equal(new LeagueStageSummary(1, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), league.Stage);
        Assert.Equal(
            [(1, 1), (2, 1), (3, 2), (4, 2)],
            ladder.Select(teamId => league.Teams.Single(t => t.Id == teamId)).Select(t => (t.Position!.Value, t.Division!.Value)));
        Assert.Equal([(1, ladder[0], ladder[1]), (2, ladder[2], ladder[3])], await GamesAsync(leagueId));
    }

    [Fact]
    public async Task With_an_odd_number_the_last_Competitor_has_the_Bye_and_no_Game()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        foreach (var name in new[] { "One", "Two", "Three" })
        {
            await ParticipantAsync(manager, clubId, leagueId, name);
        }
        var ladder = (await ProposalAsync(manager, leagueId)).Competitors.Select(c => c.TeamId).ToList();

        Assert.Equal(HttpStatusCode.NoContent, (await StartAsync(manager, leagueId, ladder)).StatusCode);

        var bye = (await LeagueAsync(manager, leagueId)).Teams.Single(t => t.Id == ladder[2]);
        Assert.Equal((3, (int?)null), (bye.Position!.Value, bye.Division));
        Assert.Equal([(1, ladder[0], ladder[1])], await GamesAsync(leagueId));
    }

    [Fact]
    public async Task A_Competitor_left_out_stays_in_the_League_without_a_place()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        foreach (var name in new[] { "One", "Two", "Three" })
        {
            await ParticipantAsync(manager, clubId, leagueId, name);
        }
        var (outClient, outId) = await ParticipantAsync(manager, clubId, leagueId, "Left out");
        var before = await LeagueAsync(manager, leagueId);
        var leftOut = TeamOf(before, outId);
        var ladder = before.Teams.Select(t => t.Id).Where(id => id != leftOut).ToList();

        Assert.Equal(HttpStatusCode.NoContent, (await StartAsync(manager, leagueId, ladder, [leftOut])).StatusCode);

        var league = await LeagueAsync(manager, leagueId);
        Assert.Contains(league.Participants, p => p.PlayerId == outId);
        Assert.Equal(((int?)null, (int?)null), league.Teams.Single(t => t.Id == leftOut) is var team ? (team.Position, team.Division) : default);
        Assert.Single(await GamesAsync(leagueId));
        var seen = (await outClient.GetFromJsonAsync<GetPlayerLeaguesResponse>($"/players/{outId}/leagues"))!.Leagues.Single();
        Assert.Equal((1, (PlayerLeaguePlace?)null), (seen.Stage!.Number, seen.Place));
    }

    [Fact]
    public async Task Starting_a_doubles_League_removes_its_Players_without_a_partner_who_may_ask_again()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, "DoublesMix");
        var players = new List<Guid>();
        foreach (var name in new[] { "A", "B", "C", "D" })
        {
            players.Add((await ParticipantAsync(manager, clubId, leagueId, name)).PlayerId);
        }
        (await manager.PostAsJsonAsync($"/leagues/{leagueId}/teams", new { firstPlayerId = players[0], secondPlayerId = players[1] })).EnsureSuccessStatusCode();
        (await manager.PostAsJsonAsync($"/leagues/{leagueId}/teams", new { firstPlayerId = players[2], secondPlayerId = players[3] })).EnsureSuccessStatusCode();
        var (alone, aloneId) = await MemberAsync(manager, clubId, "Alone");
        (await alone.PostAsJsonAsync($"/leagues/{leagueId}/join-requests", new { playerId = aloneId })).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/leagues/{leagueId}/join-requests/{aloneId}/approval", null)).EnsureSuccessStatusCode();
        var proposal = await ProposalAsync(manager, leagueId);

        var started = await StartAsync(manager, leagueId, proposal.Competitors.Select(c => c.TeamId), removed: [aloneId]);

        Assert.Equal(HttpStatusCode.NoContent, started.StatusCode);
        var league = await LeagueAsync(manager, leagueId);
        Assert.Equal(4, league.Participants.Count);
        Assert.DoesNotContain(league.JoinRequests, r => r.PlayerId == aloneId);
        Assert.Equal(HttpStatusCode.NoContent, (await alone.PostAsJsonAsync($"/leagues/{leagueId}/join-requests", new { playerId = aloneId })).StatusCode);
    }

    [Fact]
    public async Task A_start_that_no_longer_matches_the_League_is_refused_and_changes_nothing()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        await ParticipantAsync(manager, clubId, leagueId, "One");
        await ParticipantAsync(manager, clubId, leagueId, "Two");
        var proposal = await ProposalAsync(manager, leagueId);
        // Someone is added after the Start screen loaded: the Manager hasn't seen them.
        await ParticipantAsync(manager, clubId, leagueId, "Added since");

        var start = await StartAsync(manager, leagueId, proposal.Competitors.Select(c => c.TeamId));

        Assert.Equal("league-changed", await ConflictTypeAsync(start));
        Assert.Null((await LeagueAsync(manager, leagueId)).Stage);
        Assert.Empty(await GamesAsync(leagueId));
    }

    [Fact]
    public async Task A_start_must_remove_exactly_the_Players_without_a_partner()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, "DoublesMix");
        var players = new List<Guid>();
        foreach (var name in new[] { "A", "B", "C", "D", "Alone" })
        {
            players.Add((await ParticipantAsync(manager, clubId, leagueId, name)).PlayerId);
        }
        (await manager.PostAsJsonAsync($"/leagues/{leagueId}/teams", new { firstPlayerId = players[0], secondPlayerId = players[1] })).EnsureSuccessStatusCode();
        (await manager.PostAsJsonAsync($"/leagues/{leagueId}/teams", new { firstPlayerId = players[2], secondPlayerId = players[3] })).EnsureSuccessStatusCode();
        var ladder = (await ProposalAsync(manager, leagueId)).Competitors.Select(c => c.TeamId).ToList();

        Assert.Equal("league-changed", await ConflictTypeAsync(await StartAsync(manager, leagueId, ladder)));
        Assert.Equal("league-changed", await ConflictTypeAsync(await StartAsync(manager, leagueId, ladder, removed: [players[0]])));
        Assert.Equal(5, (await LeagueAsync(manager, leagueId)).Participants.Count);
    }

    [Fact]
    public async Task A_League_starts_once()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        await ParticipantAsync(manager, clubId, leagueId, "One");
        await ParticipantAsync(manager, clubId, leagueId, "Two");
        var ladder = (await ProposalAsync(manager, leagueId)).Competitors.Select(c => c.TeamId).ToList();
        (await StartAsync(manager, leagueId, ladder)).EnsureSuccessStatusCode();

        Assert.Equal("already-started", await ConflictTypeAsync(await StartAsync(manager, leagueId, ladder)));
        Assert.Equal("already-started", await ConflictTypeAsync(await manager.GetAsync($"/leagues/{leagueId}/start")));
        Assert.Single(await GamesAsync(leagueId));
    }

    [Fact]
    public async Task Two_Managers_starting_at_once_open_one_Stage()
    {
        using var manager = await factory.CreateManagerClientAsync();
        using var other = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        await ParticipantAsync(manager, clubId, leagueId, "One");
        await ParticipantAsync(manager, clubId, leagueId, "Two");
        var ladder = (await ProposalAsync(manager, leagueId)).Competitors.Select(c => c.TeamId).ToList();

        var results = await Task.WhenAll(StartAsync(manager, leagueId, ladder), StartAsync(other, leagueId, ladder));

        Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict }.Order(), results.Select(r => r.StatusCode).Order());
        Assert.Single(await GamesAsync(leagueId));
    }

    [Theory]
    [InlineData("SinglesMixed", "Knockout", "knockout-not-yet")]
    [InlineData("Foursomes", "Divisional", "foursomes-not-yet")]
    public async Task Knockout_and_Foursomes_Leagues_cant_start_yet(string type, string structure, string refusal)
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, type, structure);
        await ParticipantAsync(manager, clubId, leagueId, "One");
        await ParticipantAsync(manager, clubId, leagueId, "Two");

        Assert.Equal(refusal, await ConflictTypeAsync(await manager.GetAsync($"/leagues/{leagueId}/start")));
        Assert.Equal(refusal, await ConflictTypeAsync(await StartAsync(manager, leagueId, [Guid.NewGuid(), Guid.NewGuid()])));
    }

    [Fact]
    public async Task A_ladder_is_at_least_two_Competitors_each_once()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        await ParticipantAsync(manager, clubId, leagueId, "One");
        await ParticipantAsync(manager, clubId, leagueId, "Two");
        var ladder = (await ProposalAsync(manager, leagueId)).Competitors.Select(c => c.TeamId).ToList();

        foreach (var bad in new[]
        {
            await StartAsync(manager, leagueId, [ladder[0]], [ladder[1]]),
            await StartAsync(manager, leagueId, [ladder[0], ladder[0]], [ladder[1]]),
            await StartAsync(manager, leagueId, ladder, [ladder[1]]),
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
            Assert.True((await bad.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.ContainsKey("ladder"));
        }
        Assert.Null((await LeagueAsync(manager, leagueId)).Stage);
    }

    [Fact]
    public async Task Only_a_Manager_starts_a_League()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (player, _) = await ParticipantAsync(manager, clubId, leagueId, "One");
        await ParticipantAsync(manager, clubId, leagueId, "Two");
        var ladder = (await ProposalAsync(manager, leagueId)).Competitors.Select(c => c.TeamId).ToList();

        Assert.Equal(HttpStatusCode.Forbidden, (await player.GetAsync($"/leagues/{leagueId}/start")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await StartAsync(player, leagueId, ladder)).StatusCode);
        Assert.Null((await LeagueAsync(manager, leagueId)).Stage);
    }

    [Fact]
    public async Task After_the_start_a_Team_on_the_ladder_is_fixed_and_anyone_else_can_still_come_and_go()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, "DoublesMix");
        var players = new List<Guid>();
        foreach (var name in new[] { "A", "B", "C", "D", "E", "F" })
        {
            players.Add((await ParticipantAsync(manager, clubId, leagueId, name)).PlayerId);
        }
        for (var i = 0; i < 6; i += 2)
        {
            (await manager.PostAsJsonAsync($"/leagues/{leagueId}/teams", new { firstPlayerId = players[i], secondPlayerId = players[i + 1] })).EnsureSuccessStatusCode();
        }
        var before = await LeagueAsync(manager, leagueId);
        var leftOut = before.Teams.Single(t => t.PlayerIds.Contains(players[4])).Id;
        var placed = before.Teams.Select(t => t.Id).Where(id => id != leftOut).ToList();
        (await StartAsync(manager, leagueId, placed, [leftOut])).EnsureSuccessStatusCode();

        // On the ladder: fixed.
        Assert.Equal("on-the-ladder", await ConflictTypeAsync(await manager.DeleteAsync($"/leagues/{leagueId}/teams/{placed[0]}")));
        Assert.Equal("on-the-ladder", await ConflictTypeAsync(await manager.DeleteAsync($"/leagues/{leagueId}/participants/{players[0]}")));

        // Left out: still free to be broken up and removed.
        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync($"/leagues/{leagueId}/teams/{leftOut}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync($"/leagues/{leagueId}/participants/{players[4]}")).StatusCode);

        // Joining after the start: asking, approving and pairing work, and the new Team isn't placed.
        var (late, lateId) = await MemberAsync(manager, clubId, "Late");
        (await late.PostAsJsonAsync($"/leagues/{leagueId}/join-requests", new { playerId = lateId })).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/leagues/{leagueId}/join-requests/{lateId}/approval", null)).EnsureSuccessStatusCode();
        var paired = await manager.PostAsJsonAsync($"/leagues/{leagueId}/teams", new { firstPlayerId = players[5], secondPlayerId = lateId });
        Assert.Equal(HttpStatusCode.Created, paired.StatusCode);
        var newTeam = (await paired.Content.ReadFromJsonAsync<PairTeamResponse>())!.Id;
        var after = await LeagueAsync(manager, leagueId);
        Assert.Equal(((int?)null, (int?)null), after.Teams.Single(t => t.Id == newTeam) is var team ? (team.Position, team.Division) : default);
        Assert.Equal([1, 1], placed.Select(id => after.Teams.Single(t => t.Id == id).Division!.Value));
        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync($"/leagues/{leagueId}/teams/{newTeam}")).StatusCode);
    }

    [Fact]
    public async Task A_Player_sees_their_Division_and_opponent_or_their_Bye()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (first, firstId) = await ParticipantAsync(manager, clubId, leagueId, "First");
        await ParticipantAsync(manager, clubId, leagueId, "Second");
        var (third, thirdId) = await ParticipantAsync(manager, clubId, leagueId, "Third");
        var ladder = (await ProposalAsync(manager, leagueId)).Competitors.Select(c => c.TeamId).ToList();
        (await StartAsync(manager, leagueId, ladder)).EnsureSuccessStatusCode();

        var firstSees = (await first.GetFromJsonAsync<GetPlayerLeaguesResponse>($"/players/{firstId}/leagues"))!.Leagues.Single();
        var thirdSees = (await third.GetFromJsonAsync<GetPlayerLeaguesResponse>($"/players/{thirdId}/leagues"))!.Leagues.Single();

        Assert.Equal(new DateOnly(2026, 12, 31), firstSees.Stage!.EndDate);
        Assert.Equal((1, "Second"), (firstSees.Place!.Division!.Value, Assert.Single(firstSees.Place.Opponents)));
        Assert.Equal(((int?)null, 0), (thirdSees.Place!.Division, thirdSees.Place.Opponents.Count));
        var listed = (await manager.GetFromJsonAsync<GetClubLeaguesResponse>($"/clubs/{clubId}/leagues"))!.Leagues.Single();
        Assert.Equal(1, listed.Stage!.Number);
    }
}
