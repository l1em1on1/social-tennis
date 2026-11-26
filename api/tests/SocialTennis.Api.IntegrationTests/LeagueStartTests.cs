using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Features.Leagues.Contracts;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// Issue #7: a Manager starts a Divisional League from a proposed ladder they
/// may reorder, cut and split into Divisions, which opens Stage 1 with every
/// pair in a Division playing once (ADR-0031); and what the ladder fixes
/// afterwards.
/// </summary>
public class LeagueStartTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
{
    private static object ALeague(string type, string structure = "Divisional", int? divisionSize = null, string? standingsMode = null) => new
    {
        name = $"{type} {structure} League",
        type,
        divisionSize,
        standingsMode,
        structure,
        startDate = "2026-12-01",
        endDate = "2027-03-31",
        stageLength = new { count = 1, unit = "Months" },
        stageRoll = structure == "Divisional" ? "Manual" : null,
        scoreFormat = new { gamesPerSet = 6, setsPerGame = 1, matchTiebreakDecider = false },
    };

    private async Task<Guid> NewClubAsync(params HttpClient[] managers)
    {
        var admin = await factory.AdminClientAsync();
        var created = await admin.PostAsJsonAsync("/clubs", new { name = $"Ladder Club {Guid.NewGuid():N}" });
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<CreateClubResponse>())!.Id;
        foreach (var manager in managers)
        {
            await factory.MakeManagerAsync(manager, id);
        }

        return id;
    }

    private static async Task<Guid> CreateLeagueAsync(
        HttpClient manager, Guid clubId, string type = "SinglesMixed", string structure = "Divisional", int? divisionSize = null)
    {
        var response = await manager.PostAsJsonAsync($"/clubs/{clubId}/leagues", ALeague(type, structure, divisionSize));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateLeagueResponse>())!.Id;
    }

    /// <summary>A fresh User with one Player, approved into the Club.</summary>
    private async Task<(HttpClient Client, Guid PlayerId)> MemberAsync(HttpClient manager, Guid clubId, string name)
    {
        var client = await factory.CreateSignedInClientAsync();
        var created = await client.PostAsJsonAsync("/players", new { name });
        var playerId = (await created.Content.ReadFromJsonAsync<CreatePlayerResponse>())!.Id;
        (await factory.AskToJoinAsync(client, clubId, playerId)).EnsureSuccessStatusCode();
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

    /// <summary>Starts the League; unless told otherwise, the whole ladder is one Division.</summary>
    private static Task<HttpResponseMessage> StartAsync(
        HttpClient manager,
        Guid leagueId,
        IEnumerable<Guid> ladder,
        IEnumerable<Guid>? leftOut = null,
        IEnumerable<Guid>? removed = null,
        IEnumerable<int>? divisions = null) =>
        manager.PostAsJsonAsync(
            $"/leagues/{leagueId}/start",
            new { ladder, divisions = divisions ?? [ladder.Count()], leftOut = leftOut ?? [], removed = removed ?? [] });

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
        return [.. games.Select(g => (g.Division, g.Sides[0], g.Sides[1])).Order()];
    }

    [Fact]
    public async Task The_proposal_is_by_Rating_then_earliest_join_and_starts_nothing()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
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
        Assert.Equal(4, proposal.DivisionSize);
        Assert.Equal([4], proposal.Divisions);
        Assert.Equal(new LeagueStageSummary(1, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), proposal.FirstStage);
        Assert.Null((await LeagueAsync(manager, leagueId)).Stage);
    }

    [Fact]
    public async Task A_doubles_proposal_orders_Teams_by_their_average_and_names_who_would_be_removed()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
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
    public async Task Starting_places_the_confirmed_order_in_the_Divisions_the_Manager_sized_and_every_pair_plays_once()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var ids = new List<Guid>();
        foreach (var name in new[] { "One", "Two", "Three", "Four", "Five" })
        {
            ids.Add((await ParticipantAsync(manager, clubId, leagueId, name)).PlayerId);
        }
        var before = await LeagueAsync(manager, leagueId);
        // The Manager reverses the proposal, and splits it into a Division of 2 over one of 3.
        var ladder = ids.AsEnumerable().Reverse().Select(id => TeamOf(before, id)).ToList();

        Assert.Equal(HttpStatusCode.NoContent, (await StartAsync(manager, leagueId, ladder, divisions: [2, 3])).StatusCode);

        var league = await LeagueAsync(manager, leagueId);
        Assert.Equal(new LeagueStageSummary(1, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), league.Stage);
        Assert.Equal(
            [(1, 1), (2, 1), (3, 2), (4, 2), (5, 2)],
            ladder.Select(teamId => league.Teams.Single(t => t.Id == teamId)).Select(t => (t.Position!.Value, t.Division!.Value)));
        Assert.Equal(
            new[]
            {
                (1, ladder[0], ladder[1]),
                (2, ladder[2], ladder[3]),
                (2, ladder[2], ladder[4]),
                (2, ladder[3], ladder[4]),
            }.Order(),
            await GamesAsync(leagueId));
    }

    [Fact]
    public async Task The_proposal_splits_by_the_Leagues_Division_size_with_no_Bye()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId, divisionSize: 2);
        foreach (var name in new[] { "One", "Two", "Three", "Four", "Five" })
        {
            await ParticipantAsync(manager, clubId, leagueId, name);
        }

        var proposal = await ProposalAsync(manager, leagueId);

        // Divisions of 2, and the fifth Team joins the bottom one rather than sitting out.
        Assert.Equal(2, proposal.DivisionSize);
        Assert.Equal([2, 3], proposal.Divisions);
        (await StartAsync(manager, leagueId, proposal.Competitors.Select(c => c.TeamId), divisions: proposal.Divisions)).EnsureSuccessStatusCode();
        var league = await LeagueAsync(manager, leagueId);
        Assert.All(league.Teams, team => Assert.NotNull(team.Division));
        Assert.Equal(4, (await GamesAsync(leagueId)).Count);
    }

    [Theory]
    [InlineData(null, 4)]
    [InlineData(3, 3)]
    public async Task A_Divisional_League_of_Teams_has_a_Division_size_4_unless_the_Manager_says(int? asked, int stored)
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);

        var leagueId = await CreateLeagueAsync(manager, clubId, divisionSize: asked);

        Assert.Equal(stored, (await LeagueAsync(manager, leagueId)).DivisionSize);
    }

    [Theory]
    [InlineData("SinglesMixed", "Knockout")]
    [InlineData("Foursomes", "Divisional")]
    public async Task Knockout_and_Foursomes_Leagues_have_no_Division_size(string type, string structure)
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);

        var leagueId = await CreateLeagueAsync(manager, clubId, type, structure);

        var league = await LeagueAsync(manager, leagueId);
        Assert.Null(league.DivisionSize);
        // Nor a Standings mode (#214): Knockout has no Standings, and Foursomes ranks by games won (#14).
        Assert.Null(league.StandingsMode);
    }

    [Theory]
    [InlineData(null, StandingsMode.Standard)]
    [InlineData("BonusPoints", StandingsMode.BonusPoints)]
    public async Task A_Divisional_League_of_Teams_ranks_as_Standard_unless_the_Manager_chooses_Bonus_points(string? asked, StandingsMode stored)
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);

        var response = await manager.PostAsJsonAsync($"/clubs/{clubId}/leagues", ALeague("DoublesMix", standingsMode: asked));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var leagueId = (await response.Content.ReadFromJsonAsync<CreateLeagueResponse>())!.Id;

        Assert.Equal(stored, (await LeagueAsync(manager, leagueId)).StandingsMode);
        var listed = (await manager.GetFromJsonAsync<GetClubLeaguesResponse>($"/clubs/{clubId}/leagues"))!.Leagues.Single();
        Assert.Equal(stored, listed.StandingsMode);
    }

    [Theory]
    [InlineData("SinglesMixed", "Knockout")]
    [InlineData("Foursomes", "Divisional")]
    [InlineData("SinglesMixed", "Divisional", "Wins")]
    public async Task A_Standings_mode_is_refused_where_there_is_none_to_choose(string type, string structure, string mode = "BonusPoints")
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);

        var response = await manager.PostAsJsonAsync($"/clubs/{clubId}/leagues", ALeague(type, structure, standingsMode: mode));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        if (mode == "BonusPoints")
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
            Assert.Contains("standingsMode", problem!.Errors.Keys);
        }
    }

    /// <summary>The StandingsMode migration's SQL on rows made the old way: Leagues with no mode.</summary>
    [Fact]
    public async Task The_migration_makes_old_Divisional_Leagues_of_Teams_Standard_and_leaves_the_rest()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var divisional = await CreateLeagueAsync(manager, clubId);
        var knockout = await CreateLeagueAsync(manager, clubId, structure: "Knockout");
        var foursomes = await CreateLeagueAsync(manager, clubId, "Foursomes");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
            await db.Leagues.Where(l => l.Id == divisional).ExecuteUpdateAsync(set => set.SetProperty(l => l.StandingsMode, (StandingsMode?)null));

            foreach (var sql in new SocialTennis.Api.Migrations.StandingsMode().UpOperations.OfType<SqlOperation>().Select(o => o.Sql))
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        Assert.Equal(StandingsMode.Standard, (await LeagueAsync(manager, divisional)).StandingsMode);
        Assert.Null((await LeagueAsync(manager, knockout)).StandingsMode);
        Assert.Null((await LeagueAsync(manager, foursomes)).StandingsMode);
    }

    [Fact]
    public async Task Divisions_hold_the_whole_ladder_with_at_least_two_each()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        foreach (var name in new[] { "One", "Two", "Three", "Four" })
        {
            await ParticipantAsync(manager, clubId, leagueId, name);
        }
        var ladder = (await ProposalAsync(manager, leagueId)).Competitors.Select(c => c.TeamId).ToList();

        // The last two overflow an int sum: still a 400, not a 500.
        foreach (var divisions in new[] { new[] { 1, 3 }, new[] { 2, 3 }, new[] { 2 }, Array.Empty<int>(), new[] { int.MaxValue, int.MaxValue } })
        {
            var bad = await StartAsync(manager, leagueId, ladder, divisions: divisions);
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
            Assert.True((await bad.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.ContainsKey("divisions"));
        }
        Assert.Null((await LeagueAsync(manager, leagueId)).Stage);
    }

    [Fact]
    public async Task A_Competitor_left_out_stays_in_the_League_without_a_place()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
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
        // The three placed are one Division, each pair playing once; the one left out plays nobody.
        Assert.Equal(3, (await GamesAsync(leagueId)).Count);
        var seen = (await outClient.GetFromJsonAsync<GetPlayerLeaguesResponse>($"/players/{outId}/leagues"))!.Leagues.Single();
        Assert.Equal((1, (PlayerLeaguePlace?)null), (seen.Stage!.Number, seen.Place));
    }

    [Fact]
    public async Task Starting_a_doubles_League_removes_its_Players_without_a_partner_who_may_ask_again()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
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
        var clubId = await NewClubAsync(manager);
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
        var clubId = await NewClubAsync(manager);
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
        var clubId = await NewClubAsync(manager);
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
        var clubId = await NewClubAsync(manager, other);
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
        var clubId = await NewClubAsync(manager);
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
        var clubId = await NewClubAsync(manager);
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
        var clubId = await NewClubAsync(manager);
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
        var clubId = await NewClubAsync(manager);
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
    public async Task A_Player_sees_their_Division_and_everyone_they_play_in_it()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (first, firstId) = await ParticipantAsync(manager, clubId, leagueId, "First");
        await ParticipantAsync(manager, clubId, leagueId, "Second");
        var (third, thirdId) = await ParticipantAsync(manager, clubId, leagueId, "Third");
        var ladder = (await ProposalAsync(manager, leagueId)).Competitors.Select(c => c.TeamId).ToList();
        (await StartAsync(manager, leagueId, ladder)).EnsureSuccessStatusCode();

        var firstSees = (await first.GetFromJsonAsync<GetPlayerLeaguesResponse>($"/players/{firstId}/leagues"))!.Leagues.Single();
        var thirdSees = (await third.GetFromJsonAsync<GetPlayerLeaguesResponse>($"/players/{thirdId}/leagues"))!.Leagues.Single();

        Assert.Equal(new DateOnly(2026, 12, 31), firstSees.Stage!.EndDate);
        Assert.Equal(1, firstSees.Place!.Division);
        Assert.Equal(["Second", "Third"], firstSees.Place.Opponents.Select(o => Assert.Single(o.Names)));
        Assert.Equal(["First", "Second"], thirdSees.Place!.Opponents.Select(o => Assert.Single(o.Names)));
        var listed = (await manager.GetFromJsonAsync<GetClubLeaguesResponse>($"/clubs/{clubId}/leagues"))!.Leagues.Single();
        Assert.Equal(1, listed.Stage!.Number);
    }

    /// <summary>
    /// The DivisionSize migration's SQL on rows made the old way: a League
    /// with no Division size, and a Stage whose places have no Division.
    /// </summary>
    [Fact]
    public async Task The_migration_gives_old_Leagues_a_Division_size_and_old_places_their_Divisions()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var knockoutId = await CreateLeagueAsync(manager, clubId, structure: "Knockout");
        foreach (var name in new[] { "One", "Two", "Three", "Four", "Five" })
        {
            await ParticipantAsync(manager, clubId, leagueId, name);
        }
        var ladder = (await ProposalAsync(manager, leagueId)).Competitors.Select(c => c.TeamId).ToList();
        (await StartAsync(manager, leagueId, ladder)).EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
            await db.Leagues.Where(l => l.Id == leagueId).ExecuteUpdateAsync(set => set.SetProperty(l => l.DivisionSize, (int?)null));
            await db.LadderPlaces.Where(p => p.LeagueId == leagueId).ExecuteUpdateAsync(set => set.SetProperty(p => p.Division, 0));

            foreach (var sql in new SocialTennis.Api.Migrations.DivisionSize().UpOperations.OfType<SqlOperation>().Select(o => o.Sql))
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        var league = await LeagueAsync(manager, leagueId);
        Assert.Equal(4, league.DivisionSize);
        // Divisions of 2 read from positions, and the old Bye, fifth, joins the Division above it.
        Assert.Equal([1, 1, 2, 2, 2], ladder.Select(teamId => league.Teams.Single(t => t.Id == teamId).Division!.Value));
        Assert.Null((await LeagueAsync(manager, knockoutId)).DivisionSize);
    }

    private static Task<HttpResponseMessage> LadderAsync(HttpClient client, Guid leagueId) =>
        client.GetAsync($"/leagues/{leagueId}/ladder");

    [Fact]
    public async Task The_ladder_shows_each_Division_from_the_top_with_its_Teams_and_every_Game_between_them()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var players = new List<(HttpClient Client, Guid PlayerId)>();
        foreach (var name in new[] { "One", "Two", "Three", "Four", "Five" })
        {
            players.Add(await ParticipantAsync(manager, clubId, leagueId, name));
        }
        var ladder = (await ProposalAsync(manager, leagueId)).Competitors.Select(c => c.TeamId).ToList();
        (await StartAsync(manager, leagueId, ladder, divisions: [2, 3])).EnsureSuccessStatusCode();

        var seen = (await (await LadderAsync(players[0].Client, leagueId)).Content.ReadFromJsonAsync<GetLeagueLadderResponse>())!;

        Assert.Equal(new LeagueStageSummary(1, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), seen.Stage);
        Assert.Equal([1, 2], seen.Divisions.Select(d => d.Number));
        Assert.Equal(
            [["One", "Two"], ["Three", "Four", "Five"]],
            seen.Divisions.Select(d => d.Competitors.Select(c => Assert.Single(c.Players).Name).ToArray()));
        Assert.Equal([1, 2, 3, 4, 5], seen.Divisions.SelectMany(d => d.Competitors).Select(c => c.Position));
        // Every pair in a Division once, the higher place as the first side, in ladder order.
        Assert.Equal([(ladder[0], ladder[1])], seen.Divisions[0].Games.Select(g => (g.FirstTeamId, g.SecondTeamId)));
        Assert.Equal(
            [(ladder[2], ladder[3]), (ladder[2], ladder[4]), (ladder[3], ladder[4])],
            seen.Divisions[1].Games.Select(g => (g.FirstTeamId, g.SecondTeamId)));
        Assert.Equal(
            (await GamesAsync(leagueId)).Count,
            seen.Divisions.Sum(d => d.Games.Count));
    }

    [Fact]
    public async Task A_doubles_ladder_names_both_Players_of_each_Team()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId, "DoublesMix");
        var players = new List<(HttpClient Client, Guid PlayerId)>();
        foreach (var name in new[] { "A", "B", "C", "D" })
        {
            players.Add(await ParticipantAsync(manager, clubId, leagueId, name));
        }
        (await manager.PostAsJsonAsync($"/leagues/{leagueId}/teams", new { firstPlayerId = players[0].PlayerId, secondPlayerId = players[1].PlayerId })).EnsureSuccessStatusCode();
        (await manager.PostAsJsonAsync($"/leagues/{leagueId}/teams", new { firstPlayerId = players[2].PlayerId, secondPlayerId = players[3].PlayerId })).EnsureSuccessStatusCode();
        (await StartAsync(manager, leagueId, (await ProposalAsync(manager, leagueId)).Competitors.Select(c => c.TeamId))).EnsureSuccessStatusCode();

        var seen = (await (await LadderAsync(players[3].Client, leagueId)).Content.ReadFromJsonAsync<GetLeagueLadderResponse>())!;

        var division = Assert.Single(seen.Divisions);
        Assert.Equal(["A B", "C D"], division.Competitors.Select(c => string.Join(" ", c.Players.Select(p => p.Name))));
        Assert.Single(division.Games);
    }

    [Fact]
    public async Task Everyone_in_the_League_sees_the_ladder_even_off_it_and_its_Clubs_managers_do_too()
    {
        using var manager = await factory.CreateManagerClientAsync();
        using var otherManager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager, otherManager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        await ParticipantAsync(manager, clubId, leagueId, "One");
        await ParticipantAsync(manager, clubId, leagueId, "Two");
        var (outClient, outId) = await ParticipantAsync(manager, clubId, leagueId, "Left out");
        var before = await LeagueAsync(manager, leagueId);
        var leftOut = TeamOf(before, outId);
        (await StartAsync(manager, leagueId, before.Teams.Select(t => t.Id).Where(id => id != leftOut), [leftOut])).EnsureSuccessStatusCode();

        var outSees = (await (await LadderAsync(outClient, leagueId)).Content.ReadFromJsonAsync<GetLeagueLadderResponse>())!;
        Assert.Equal([outId], outSees.YourPlayerIds);
        var managerSees = (await (await LadderAsync(otherManager, leagueId)).Content.ReadFromJsonAsync<GetLeagueLadderResponse>())!;
        Assert.Equal(2, Assert.Single(managerSees.Divisions).Competitors.Count);
        Assert.DoesNotContain(managerSees.Divisions.SelectMany(d => d.Competitors), c => c.TeamId == leftOut);
        Assert.Empty(managerSees.YourPlayerIds);
    }

    [Fact]
    public async Task Before_the_start_the_ladder_is_empty()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (player, _) = await ParticipantAsync(manager, clubId, leagueId, "Waiting");

        var seen = (await (await LadderAsync(player, leagueId)).Content.ReadFromJsonAsync<GetLeagueLadderResponse>())!;

        Assert.Equal(((LeagueStageSummary?)null, 0), (seen.Stage, seen.Divisions.Count));
        Assert.Equal("SinglesMixed Divisional League", seen.Name);
    }

    [Fact]
    public async Task A_Player_not_in_the_League_cant_see_its_ladder()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        await ParticipantAsync(manager, clubId, leagueId, "One");
        await ParticipantAsync(manager, clubId, leagueId, "Two");
        (await StartAsync(manager, leagueId, (await ProposalAsync(manager, leagueId)).Competitors.Select(c => c.TeamId))).EnsureSuccessStatusCode();
        var (member, _) = await MemberAsync(manager, clubId, "Club member only");

        Assert.Equal(HttpStatusCode.NotFound, (await LadderAsync(member, leagueId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await LadderAsync(member, Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task The_ladder_says_which_of_the_Users_Players_are_in_the_League_even_one_who_left_the_Club()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (parent, parentId) = await ParticipantAsync(manager, clubId, leagueId, "Parent");
        await ParticipantAsync(manager, clubId, leagueId, "Other");
        // A second Player on the same login, in the Club but not in the League.
        var created = await parent.PostAsJsonAsync("/players", new { name = "Child" });
        var childId = (await created.Content.ReadFromJsonAsync<CreatePlayerResponse>())!.Id;
        (await StartAsync(manager, leagueId, (await ProposalAsync(manager, leagueId)).Competitors.Select(c => c.TeamId))).EnsureSuccessStatusCode();

        var seen = (await (await LadderAsync(parent, leagueId)).Content.ReadFromJsonAsync<GetLeagueLadderResponse>())!;
        Assert.Equal([parentId], seen.YourPlayerIds);
        Assert.DoesNotContain(childId, seen.YourPlayerIds);

        // Leaving the Club doesn't take a Player out of the League (#21): they still see it, as one of theirs.
        (await parent.DeleteAsync($"/players/{parentId}/clubs/{clubId}")).EnsureSuccessStatusCode();
        var after = (await (await LadderAsync(parent, leagueId)).Content.ReadFromJsonAsync<GetLeagueLadderResponse>())!;
        Assert.Equal([parentId], after.YourPlayerIds);
    }
}
