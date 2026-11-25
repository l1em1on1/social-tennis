using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Features.Leagues.Contracts;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// Issue #5: a Manager creates a League; Players see it, ask to join, and a
/// Manager approves, rejects, or adds them directly.
/// </summary>
public class LeaguesTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
{
    private static object ALeague(
        string type = "SinglesMixed",
        string structure = "Divisional",
        string? stageRoll = "Manual",
        object? stageLength = null,
        object? scoreFormat = null,
        string name = "Winter Singles") => new
        {
            name,
            type,
            structure,
            startDate = "2026-12-01",
            endDate = "2027-03-31",
            stageLength = stageLength ?? new { count = 1, unit = "Months" },
            stageRoll,
            scoreFormat = scoreFormat ?? new { gamesPerSet = 6, setsPerGame = 1, matchTiebreakDecider = false },
        };

    /// <summary>A Club of its own, so another test's Leagues never show up in this one's lists.</summary>
    private async Task<Guid> NewClubAsync(params HttpClient[] managers)
    {
        var admin = await factory.AdminClientAsync();
        var created = await admin.PostAsJsonAsync("/clubs", new { name = $"League Club {Guid.NewGuid():N}" });
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<CreateClubResponse>())!.Id;
        foreach (var manager in managers)
        {
            await factory.MakeManagerAsync(manager, id);
        }

        return id;
    }

    private static async Task<Guid> CreateLeagueAsync(HttpClient manager, Guid clubId, object? body = null)
    {
        var response = await manager.PostAsJsonAsync($"/clubs/{clubId}/leagues", body ?? ALeague());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateLeagueResponse>())!.Id;
    }

    /// <summary>A fresh User with one Player, approved into the Club.</summary>
    private async Task<(HttpClient Client, Guid PlayerId)> MemberAsync(HttpClient manager, Guid clubId, string name = "Sam")
    {
        var client = await factory.CreateSignedInClientAsync();
        var created = await client.PostAsJsonAsync("/players", new { name });
        var playerId = (await created.Content.ReadFromJsonAsync<CreatePlayerResponse>())!.Id;
        (await factory.AskToJoinAsync(client, clubId, playerId)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/clubs/{clubId}/members/{playerId}/approval", null)).EnsureSuccessStatusCode();
        return (client, playerId);
    }

    private static async Task<GetLeagueResponse> LeagueAsync(HttpClient manager, Guid leagueId) =>
        (await manager.GetFromJsonAsync<GetLeagueResponse>($"/leagues/{leagueId}"))!;

    private static async Task<IReadOnlyList<PlayerLeague>> PlayerLeaguesAsync(HttpClient client, Guid playerId) =>
        (await client.GetFromJsonAsync<GetPlayerLeaguesResponse>($"/players/{playerId}/leagues"))!.Leagues;

    private static Task<HttpResponseMessage> AskAsync(HttpClient client, Guid leagueId, Guid playerId) =>
        client.PostAsJsonAsync($"/leagues/{leagueId}/join-requests", new { playerId });

    private static async Task<ValidationProblemDetails> ValidationProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!;
    }

    [Fact]
    public async Task A_Divisional_League_is_created_with_its_Stage_length_roll_mode_and_Score_format()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);

        var leagueId = await CreateLeagueAsync(manager, clubId, ALeague(
            stageRoll: "Automatic",
            scoreFormat: new { gamesPerSet = 9, setsPerGame = 3, matchTiebreakDecider = true }));

        var league = await LeagueAsync(manager, leagueId);
        Assert.Equal(("Winter Singles", LeagueType.SinglesMixed, LeagueStructure.Divisional), (league.Name, league.Type, league.Structure));
        Assert.Equal((new DateOnly(2026, 12, 1), new DateOnly(2027, 3, 31)), (league.StartDate, league.EndDate));
        Assert.Equal(new StageLength(1, StageLengthUnit.Months), league.StageLength);
        Assert.Equal(StageRoll.Automatic, league.StageRoll);
        Assert.Equal(new ScoreFormat(9, 3, true), league.ScoreFormat);
        Assert.Empty(league.Participants);
        Assert.Empty(league.JoinRequests);

        var listed = Assert.Single((await manager.GetFromJsonAsync<GetClubLeaguesResponse>($"/clubs/{clubId}/leagues"))!.Leagues);
        Assert.Equal((leagueId, 0, 0), (listed.Id, listed.ParticipantCount, listed.PendingRequestCount));
    }

    [Fact]
    public async Task A_Knockout_League_has_a_Stage_length_per_round_and_no_roll_mode()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);

        var leagueId = await CreateLeagueAsync(manager, clubId, ALeague(
            type: "DoublesMen",
            structure: "Knockout",
            stageRoll: null,
            stageLength: new { count = 3, unit = "Weeks" }));

        var league = await LeagueAsync(manager, leagueId);
        Assert.Equal((LeagueType.DoublesMen, LeagueStructure.Knockout), (league.Type, league.Structure));
        Assert.Equal(new StageLength(3, StageLengthUnit.Weeks), league.StageLength);
        Assert.Null(league.StageRoll);
    }

    [Fact]
    public async Task Every_Type_except_Foursomes_can_be_Knockout_and_every_Type_can_be_Divisional()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);

        foreach (var type in Enum.GetNames<LeagueType>())
        {
            await CreateLeagueAsync(manager, clubId, ALeague(type: type));
            if (type != nameof(LeagueType.Foursomes))
            {
                await CreateLeagueAsync(manager, clubId, ALeague(type: type, structure: "Knockout", stageRoll: null));
            }
        }

        var leagues = (await manager.GetFromJsonAsync<GetClubLeaguesResponse>($"/clubs/{clubId}/leagues"))!.Leagues;
        Assert.Equal(13, leagues.Count);
    }

    [Fact]
    public async Task Foursomes_Knockout_is_refused_with_a_clear_message()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);

        var response = await manager.PostAsJsonAsync(
            $"/clubs/{clubId}/leagues", ALeague(type: "Foursomes", structure: "Knockout", stageRoll: null));

        var problem = await ValidationProblemAsync(response);
        Assert.Contains("can't be Knockout", Assert.Single(problem.Errors["structure"]));
        Assert.Empty((await manager.GetFromJsonAsync<GetClubLeaguesResponse>($"/clubs/{clubId}/leagues"))!.Leagues);
    }

    [Fact]
    public async Task A_Match_Tiebreak_on_a_one_Set_League_is_refused_with_a_clear_message()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);

        var response = await manager.PostAsJsonAsync($"/clubs/{clubId}/leagues", ALeague(
            scoreFormat: new { gamesPerSet = 6, setsPerGame = 1, matchTiebreakDecider = true }));

        var problem = await ValidationProblemAsync(response);
        Assert.Contains("best-of-three", Assert.Single(problem.Errors["scoreFormat.matchTiebreakDecider"]));
    }

    [Fact]
    public async Task A_Divisional_League_needs_a_roll_mode_and_a_Knockout_one_refuses_it()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);

        var divisional = await manager.PostAsJsonAsync($"/clubs/{clubId}/leagues", ALeague(stageRoll: null));
        Assert.True((await ValidationProblemAsync(divisional)).Errors.ContainsKey("stageRoll"));

        var knockout = await manager.PostAsJsonAsync(
            $"/clubs/{clubId}/leagues", ALeague(structure: "Knockout", stageRoll: "Automatic"));
        Assert.True((await ValidationProblemAsync(knockout)).Errors.ContainsKey("stageRoll"));
    }

    [Fact]
    public async Task Only_a_Manager_creates_a_League_adds_Players_or_decides_requests()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (player, playerId) = await MemberAsync(manager, clubId);
        (await AskAsync(player, leagueId, playerId)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await player.PostAsJsonAsync($"/clubs/{clubId}/leagues", ALeague())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.GetAsync($"/clubs/{clubId}/leagues")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.GetAsync($"/leagues/{leagueId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.PutAsync($"/leagues/{leagueId}/participants/{playerId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.PutAsync($"/leagues/{leagueId}/join-requests/{playerId}/approval", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.PutAsync($"/leagues/{leagueId}/join-requests/{playerId}/rejection", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.PutAsJsonAsync($"/leagues/{leagueId}", new { name = "Mine" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.DeleteAsync($"/leagues/{leagueId}/participants/{playerId}")).StatusCode);

        // Nothing changed.
        var league = await LeagueAsync(manager, leagueId);
        Assert.Equal("Winter Singles", league.Name);
        Assert.Empty(league.Participants);
        Assert.Equal(LeagueJoinRequestStatus.Pending, Assert.Single(league.JoinRequests).Status);
    }

    [Fact]
    public async Task A_Player_sees_their_Leagues_apart_from_the_ones_they_could_join()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var mine = await CreateLeagueAsync(manager, clubId, ALeague(name: "Mine"));
        var open = await CreateLeagueAsync(manager, clubId, ALeague(name: "Open"));
        var (player, playerId) = await MemberAsync(manager, clubId);
        (await manager.PutAsync($"/leagues/{mine}/participants/{playerId}", null)).EnsureSuccessStatusCode();

        var leagues = await PlayerLeaguesAsync(player, playerId);

        Assert.Equal([mine], leagues.Where(l => l.Joined).Select(l => l.Id));
        Assert.Equal([open], leagues.Where(l => !l.Joined).Select(l => l.Id));
        Assert.All(leagues, l => Assert.Null(l.JoinRequest));
    }

    [Fact]
    public async Task An_approved_request_makes_the_Player_a_participant_joined_at_the_request_time()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (player, playerId) = await MemberAsync(manager, clubId, "Robin");

        Assert.Equal(HttpStatusCode.NoContent, (await AskAsync(player, leagueId, playerId)).StatusCode);

        // Both sides see it pending.
        var asked = Assert.Single(await PlayerLeaguesAsync(player, playerId));
        Assert.Equal((false, LeagueJoinRequestStatus.Pending), (asked.Joined, asked.JoinRequest!.Status));
        var pending = Assert.Single((await LeagueAsync(manager, leagueId)).JoinRequests);
        Assert.Equal(("Robin", LeagueJoinRequestStatus.Pending), (pending.Name, pending.Status));
        Assert.Equal(1, Assert.Single((await manager.GetFromJsonAsync<GetClubLeaguesResponse>($"/clubs/{clubId}/leagues"))!.Leagues).PendingRequestCount);

        var approve = await manager.PutAsync($"/leagues/{leagueId}/join-requests/{playerId}/approval", null);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        var league = await LeagueAsync(manager, leagueId);
        var participant = Assert.Single(league.Participants);
        Assert.Equal((playerId, pending.RequestedAt), (participant.PlayerId, participant.JoinedAt));
        Assert.Equal(LeagueJoinRequestStatus.Approved, Assert.Single(league.JoinRequests).Status);

        var joined = Assert.Single(await PlayerLeaguesAsync(player, playerId));
        Assert.Equal((true, LeagueJoinRequestStatus.Approved), (joined.Joined, joined.JoinRequest!.Status));

        // Approving again changes nothing; rejecting it now is refused.
        Assert.Equal(HttpStatusCode.NoContent, (await manager.PutAsync($"/leagues/{leagueId}/join-requests/{playerId}/approval", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PutAsync($"/leagues/{leagueId}/join-requests/{playerId}/rejection", null)).StatusCode);
        Assert.Single((await LeagueAsync(manager, leagueId)).Participants);
    }

    [Fact]
    public async Task A_rejected_Player_is_not_a_participant_and_sees_the_outcome()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (player, playerId) = await MemberAsync(manager, clubId);
        (await AskAsync(player, leagueId, playerId)).EnsureSuccessStatusCode();

        var reject = await manager.PutAsync($"/leagues/{leagueId}/join-requests/{playerId}/rejection", null);
        Assert.Equal(HttpStatusCode.NoContent, reject.StatusCode);

        Assert.Empty((await LeagueAsync(manager, leagueId)).Participants);
        var seen = Assert.Single(await PlayerLeaguesAsync(player, playerId));
        Assert.False(seen.Joined);
        Assert.Equal(LeagueJoinRequestStatus.Rejected, seen.JoinRequest!.Status);
        Assert.NotNull(seen.JoinRequest.DecidedAt);

        // Approving it afterwards is refused: a Manager adds them directly instead.
        var approve = await manager.PutAsync($"/leagues/{leagueId}/join-requests/{playerId}/approval", null);
        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
    }

    [Fact]
    public async Task A_Player_cannot_ask_to_join_the_same_League_twice()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (player, playerId) = await MemberAsync(manager, clubId);

        (await AskAsync(player, leagueId, playerId)).EnsureSuccessStatusCode();
        var again = await AskAsync(player, leagueId, playerId);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("already-asked", (await again.Content.ReadFromJsonAsync<ProblemDetails>())!.Type);

        // Nor after a rejection.
        (await manager.PutAsync($"/leagues/{leagueId}/join-requests/{playerId}/rejection", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await AskAsync(player, leagueId, playerId)).StatusCode);

        Assert.Single((await LeagueAsync(manager, leagueId)).JoinRequests);
    }

    [Fact]
    public async Task A_participant_cannot_ask_to_join()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (player, playerId) = await MemberAsync(manager, clubId);
        (await manager.PutAsync($"/leagues/{leagueId}/participants/{playerId}", null)).EnsureSuccessStatusCode();

        var ask = await AskAsync(player, leagueId, playerId);
        Assert.Equal(HttpStatusCode.Conflict, ask.StatusCode);
        Assert.Equal("already-in-league", (await ask.Content.ReadFromJsonAsync<ProblemDetails>())!.Type);
    }

    [Fact]
    public async Task A_Player_cannot_join_a_League_at_a_Club_they_are_not_a_member_of()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);

        // Not a member at all, and only asking to be one.
        using var outsider = await factory.CreateSignedInClientAsync();
        var created = await outsider.PostAsJsonAsync("/players", new { name = "Outsider" });
        var outsiderId = (await created.Content.ReadFromJsonAsync<CreatePlayerResponse>())!.Id;
        Assert.Equal(HttpStatusCode.NotFound, (await AskAsync(outsider, leagueId, outsiderId)).StatusCode);
        Assert.Empty(await PlayerLeaguesAsync(outsider, outsiderId));

        (await factory.AskToJoinAsync(outsider, clubId, outsiderId)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await AskAsync(outsider, leagueId, outsiderId)).StatusCode);
        Assert.Empty(await PlayerLeaguesAsync(outsider, outsiderId));

        // Nor can a Manager add them.
        var add = await manager.PutAsync($"/leagues/{leagueId}/participants/{outsiderId}", null);
        Assert.Equal(HttpStatusCode.Conflict, add.StatusCode);
        Assert.Empty((await LeagueAsync(manager, leagueId)).JoinRequests);
    }

    [Fact]
    public async Task A_User_cannot_ask_or_list_for_a_Player_they_do_not_hold()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (_, playerId) = await MemberAsync(manager, clubId);
        using var someoneElse = await factory.CreateSignedInClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await AskAsync(someoneElse, leagueId, playerId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await someoneElse.GetAsync($"/players/{playerId}/leagues")).StatusCode);
    }

    [Fact]
    public async Task A_Manager_adds_a_Club_Player_straight_in_joined_at_the_add_time()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (_, playerId) = await MemberAsync(manager, clubId);

        // A Player added by name and email, who hasn't signed in, can be put in too.
        var added = await manager.PostAsJsonAsync(
            $"/clubs/{clubId}/players", new { name = "Rosa", email = $"rosa-{Guid.NewGuid():N}@example.test" });
        var rosaId = (await added.Content.ReadFromJsonAsync<AddClubPlayerResponse>())!.PlayerId;

        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.PutAsync($"/leagues/{leagueId}/participants/{playerId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.PutAsync($"/leagues/{leagueId}/participants/{rosaId}", null)).StatusCode);
        // Adding again changes nothing.
        Assert.Equal(HttpStatusCode.NoContent, (await manager.PutAsync($"/leagues/{leagueId}/participants/{playerId}", null)).StatusCode);

        var league = await LeagueAsync(manager, leagueId);
        Assert.Equal([playerId, rosaId], league.Participants.Select(p => p.PlayerId));
        Assert.All(league.Participants, p => Assert.True(p.JoinedAt > before));
        Assert.Empty(league.JoinRequests);
    }

    [Fact]
    public async Task Adding_a_Player_who_asked_approves_the_request_and_keeps_the_request_time()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (player, playerId) = await MemberAsync(manager, clubId);
        (await AskAsync(player, leagueId, playerId)).EnsureSuccessStatusCode();
        var requestedAt = Assert.Single((await LeagueAsync(manager, leagueId)).JoinRequests).RequestedAt;

        (await manager.PutAsync($"/leagues/{leagueId}/participants/{playerId}", null)).EnsureSuccessStatusCode();

        var league = await LeagueAsync(manager, leagueId);
        Assert.Equal(requestedAt, Assert.Single(league.Participants).JoinedAt);
        Assert.Equal(LeagueJoinRequestStatus.Approved, Assert.Single(league.JoinRequests).Status);
    }

    [Fact]
    public async Task Leagues_of_a_Club_that_does_not_exist_are_a_404()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var missing = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsJsonAsync($"/clubs/{missing}/leagues", ALeague())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/clubs/{missing}/leagues")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/leagues/{missing}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PutAsJsonAsync($"/leagues/{missing}", new { name = "Spring" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.DeleteAsync($"/leagues/{missing}/participants/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task A_Manager_renames_a_League_and_nothing_else_about_it_changes()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId, ALeague(name: "Winter Singles"));
        var (asker, askerId) = await MemberAsync(manager, clubId, "Robin");
        var (player, playerId) = await MemberAsync(manager, clubId, "Sam");
        (await AskAsync(asker, leagueId, askerId)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/leagues/{leagueId}/participants/{playerId}", null)).EnsureSuccessStatusCode();
        var before = await LeagueAsync(manager, leagueId);

        var response = await manager.PutAsJsonAsync($"/leagues/{leagueId}", new { name = "  Spring Singles  " });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = await LeagueAsync(manager, leagueId);
        Assert.Equal("Spring Singles", after.Name);
        Assert.Equal(before with { Name = after.Name, Participants = after.Participants, JoinRequests = after.JoinRequests, Teams = after.Teams }, after);
        Assert.Equal(before.Participants, after.Participants);
        Assert.Equal(before.JoinRequests, after.JoinRequests);
        Assert.Equal(
            before.Teams.Select(t => (t.Id, string.Join(",", t.PlayerIds), t.Rating)),
            after.Teams.Select(t => (t.Id, string.Join(",", t.PlayerIds), t.Rating)));

        // Players and the Club's list see the new name.
        Assert.Equal("Spring Singles", Assert.Single(await PlayerLeaguesAsync(player, playerId)).Name);
        Assert.Equal("Spring Singles", Assert.Single(await PlayerLeaguesAsync(asker, askerId)).Name);
        var clubLeagues = await manager.GetFromJsonAsync<GetClubLeaguesResponse>($"/clubs/{clubId}/leagues");
        Assert.Equal("Spring Singles", Assert.Single(clubLeagues!.Leagues).Name);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("Tab\tin the name")]
    public async Task Renaming_a_League_to_a_blank_or_unprintable_name_is_refused(string name)
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId, ALeague(name: "Winter Singles"));

        var problem = await ValidationProblemAsync(await manager.PutAsJsonAsync($"/leagues/{leagueId}", new { name }));

        Assert.True(problem.Errors.ContainsKey("name"));
        Assert.Equal("Winter Singles", (await LeagueAsync(manager, leagueId)).Name);
    }

    [Fact]
    public async Task Renaming_a_League_to_more_than_200_characters_is_refused()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);

        var problem = await ValidationProblemAsync(
            await manager.PutAsJsonAsync($"/leagues/{leagueId}", new { name = new string('a', League.NameMaxLength + 1) }));

        Assert.Contains("200", Assert.Single(problem.Errors["name"]));
    }

    [Fact]
    public async Task A_Manager_removes_a_participant_who_can_then_ask_again()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (player, playerId) = await MemberAsync(manager, clubId, "Robin");
        var (other, otherId) = await MemberAsync(manager, clubId, "Sam");
        (await AskAsync(player, leagueId, playerId)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/leagues/{leagueId}/join-requests/{playerId}/approval", null)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/leagues/{leagueId}/participants/{otherId}", null)).EnsureSuccessStatusCode();

        var response = await manager.DeleteAsync($"/leagues/{leagueId}/participants/{playerId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var league = await LeagueAsync(manager, leagueId);
        Assert.Equal([otherId], league.Participants.Select(p => p.PlayerId));
        Assert.DoesNotContain(league.JoinRequests, r => r.PlayerId == playerId);
        var seen = Assert.Single(await PlayerLeaguesAsync(player, playerId));
        Assert.Equal((false, (PlayerLeagueJoinRequest?)null), (seen.Joined, seen.JoinRequest));

        // Back where they were before asking: they may ask again, and a Manager decides.
        Assert.Equal(HttpStatusCode.NoContent, (await AskAsync(player, leagueId, playerId)).StatusCode);
        Assert.Equal(
            LeagueJoinRequestStatus.Pending,
            Assert.Single((await LeagueAsync(manager, leagueId)).JoinRequests, r => r.PlayerId == playerId).Status);
    }

    [Fact]
    public async Task Removing_a_Player_who_is_not_in_the_League_changes_nothing()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (player, playerId) = await MemberAsync(manager, clubId);
        (await AskAsync(player, leagueId, playerId)).EnsureSuccessStatusCode();

        var response = await manager.DeleteAsync($"/leagues/{leagueId}/participants/{playerId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(LeagueJoinRequestStatus.Pending, Assert.Single((await LeagueAsync(manager, leagueId)).JoinRequests).Status);
    }

    [Fact]
    public async Task A_Player_turned_down_then_added_then_removed_still_cannot_ask()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (player, playerId) = await MemberAsync(manager, clubId);
        (await AskAsync(player, leagueId, playerId)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/leagues/{leagueId}/join-requests/{playerId}/rejection", null)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/leagues/{leagueId}/participants/{playerId}", null)).EnsureSuccessStatusCode();

        (await manager.DeleteAsync($"/leagues/{leagueId}/participants/{playerId}")).EnsureSuccessStatusCode();

        Assert.Equal(LeagueJoinRequestStatus.Rejected, Assert.Single(await PlayerLeaguesAsync(player, playerId)).JoinRequest!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await AskAsync(player, leagueId, playerId)).StatusCode);
    }
}
