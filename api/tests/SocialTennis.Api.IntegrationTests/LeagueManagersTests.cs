using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Auth.Contracts;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Features.Games.Contracts;
using SocialTennis.Api.Features.Leagues.Contracts;
using SocialTennis.Api.Features.Players.Contracts;
using SocialTennis.Api.Features.Users.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// League managers (#220, ADR-0034): a role that creates Leagues, and Leagues
/// assigned to run. Who may call which route is <see cref="ManagerAccessTests"/>';
/// this is what each gets.
/// </summary>
public class LeagueManagersTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
{
    private sealed record Person(HttpClient Client, Guid UserId, Guid PlayerId);

    private async Task<(Guid ClubId, HttpClient Manager)> ClubAsync()
    {
        var admin = await factory.AdminClientAsync();
        var created = await admin.PostAsJsonAsync("/clubs", new { name = $"League Club {Guid.NewGuid():N}" });
        var clubId = (await created.Content.ReadFromJsonAsync<CreateClubResponse>())!.Id;
        return (clubId, await factory.CreateManagerClientAsync(clubId));
    }

    /// <summary>A User with a Player, approved into the Club unless told otherwise.</summary>
    private async Task<Person> MemberAsync(HttpClient manager, Guid clubId, string name, bool approved = true)
    {
        var client = await factory.CreateSignedInClientAsync();
        var created = await client.PostAsJsonAsync("/players", new { name });
        var playerId = (await created.Content.ReadFromJsonAsync<CreatePlayerResponse>())!.Id;
        (await factory.AskToJoinAsync(client, clubId, playerId)).EnsureSuccessStatusCode();
        if (approved)
        {
            (await manager.PutAsync($"/clubs/{clubId}/members/{playerId}/approval", null)).EnsureSuccessStatusCode();
        }

        var me = (await client.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!;
        return new Person(client, me.UserId, playerId);
    }

    private static async Task<HttpResponseMessage> CreateAsync(HttpClient client, Guid clubId, string name = "Winter Singles") =>
        await client.PostAsJsonAsync($"/clubs/{clubId}/leagues", new
        {
            name,
            type = "SinglesMixed",
            structure = "Divisional",
            startDate = "2030-12-01",
            endDate = "2031-03-31",
            stageLength = new { count = 1, unit = "Months" },
            stageRoll = "Manual",
            scoreFormat = new { gamesPerSet = 6, setsPerGame = 1, matchTiebreakDecider = false },
        });

    private static async Task<Guid> LeagueAsync(HttpClient client, Guid clubId, string name = "Winter Singles")
    {
        var created = await CreateAsync(client, clubId, name);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<CreateLeagueResponse>())!.Id;
    }

    private static async Task RoleAsync(HttpClient manager, Guid clubId, Guid userId) =>
        (await manager.PutAsync($"/clubs/{clubId}/league-managers/{userId}", null)).EnsureSuccessStatusCode();

    private static async Task AssignAsync(HttpClient manager, Guid leagueId, Guid userId) =>
        (await manager.PutAsync($"/leagues/{leagueId}/managers/{userId}", null)).EnsureSuccessStatusCode();

    private static async Task<IEnumerable<Guid>> LeagueIdsAsync(HttpClient client, Guid clubId) =>
        (await client.GetFromJsonAsync<GetClubLeaguesResponse>($"/clubs/{clubId}/leagues"))!.Leagues.Select(l => l.Id).Order();

    private static async Task<GetLeagueResponse> LeagueSeenAsync(HttpClient client, Guid leagueId) =>
        (await client.GetFromJsonAsync<GetLeagueResponse>($"/leagues/{leagueId}"))!;

    private static async Task<bool> ManagesAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!.Manages;

    private static async Task<bool> ManagesClubAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!.ManagesClub;

    private static async Task<string?> ProblemTypeAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Type;
    }

    [Fact]
    public async Task A_League_manager_creates_a_League_runs_it_and_sees_no_other()
    {
        var (clubId, manager) = await ClubAsync();
        var theirs = await LeagueAsync(manager, clubId, "The Club's");
        var lee = await MemberAsync(manager, clubId, "Lee");
        await RoleAsync(manager, clubId, lee.UserId);
        // They run something, and not a Club: nobody is let into one by them.
        Assert.Equal((true, false), (await ManagesAsync(lee.Client), await ManagesClubAsync(lee.Client)));
        Assert.Equal((true, true), (await ManagesAsync(manager), await ManagesClubAsync(manager)));

        var own = await LeagueAsync(lee.Client, clubId, "Lee's");

        Assert.Equal([own], await LeagueIdsAsync(lee.Client, clubId));
        Assert.Equal(new[] { theirs, own }.Order(), await LeagueIdsAsync(manager, clubId));
        Assert.Equal(HttpStatusCode.OK, (await lee.Client.GetAsync($"/leagues/{own}")).StatusCode);
        (await lee.Client.PutAsJsonAsync($"/leagues/{own}", new { name = "Lee's League" })).EnsureSuccessStatusCode();
        // A member of the Club, so they can see there is another League, and not touch it.
        Assert.Equal(HttpStatusCode.Forbidden, (await lee.Client.GetAsync($"/leagues/{theirs}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await lee.Client.GetAsync($"/clubs/{clubId}/members")).StatusCode);
        // Their own list is all of a Club's that they're given; nothing is said of the rest.
        var club = (await lee.Client.GetFromJsonAsync<GetClubsResponse>("/clubs"))!.Clubs.Single();
        Assert.Equal((clubId, false, true), (club.Id, club.CanManage, club.CanCreateLeagues));
    }

    [Fact]
    public async Task Creating_a_League_gives_a_League_manager_it_and_a_Club_manager_nothing()
    {
        var (clubId, manager) = await ClubAsync();
        var lee = await MemberAsync(manager, clubId, "Lee");
        await RoleAsync(manager, clubId, lee.UserId);

        var byManager = await LeagueSeenAsync(manager, await LeagueAsync(manager, clubId, "Manager's"));
        var byLee = await LeagueSeenAsync(manager, await LeagueAsync(lee.Client, clubId, "Lee's"));
        var byAdmin = await LeagueSeenAsync(manager, await LeagueAsync(await factory.AdminClientAsync(), clubId, "Admin's"));

        Assert.Empty(byManager.Managers);
        Assert.Empty(byAdmin.Managers);
        Assert.Equal([lee.UserId], byLee.Managers.Select(m => m.UserId));
    }

    [Fact]
    public async Task Someone_assigned_to_a_League_runs_that_one_alone_and_gives_nobody_else_a_hand()
    {
        var (clubId, manager) = await ClubAsync();
        var (theirs, others) = (await LeagueAsync(manager, clubId, "Theirs"), await LeagueAsync(manager, clubId, "Others"));
        var ada = await MemberAsync(manager, clubId, "Ada");
        var bo = await MemberAsync(manager, clubId, "Bo");
        Assert.False(await ManagesAsync(ada.Client));

        await AssignAsync(manager, theirs, ada.UserId);

        Assert.True(await ManagesAsync(ada.Client));
        Assert.Equal([theirs], await LeagueIdsAsync(ada.Client, clubId));
        Assert.Equal(HttpStatusCode.OK, (await ada.Client.GetAsync($"/leagues/{theirs}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ada.Client.GetAsync($"/leagues/{others}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateAsync(ada.Client, clubId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ada.Client.PutAsync($"/leagues/{theirs}/managers/{bo.UserId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ada.Client.DeleteAsync($"/leagues/{theirs}/managers/{ada.UserId}")).StatusCode);
        Assert.False((await LeagueSeenAsync(ada.Client, theirs)).CanAssignManagers);
        var club = (await ada.Client.GetFromJsonAsync<GetClubsResponse>("/clubs"))!.Clubs.Single();
        Assert.Equal((false, false), (club.CanManage, club.CanCreateLeagues));

        (await manager.DeleteAsync($"/leagues/{theirs}/managers/{ada.UserId}")).EnsureSuccessStatusCode();

        Assert.False(await ManagesAsync(ada.Client));
        Assert.Equal(HttpStatusCode.Forbidden, (await ada.Client.GetAsync($"/leagues/{theirs}")).StatusCode);
    }

    [Fact]
    public async Task Only_someone_with_a_Player_in_the_Club_can_be_given_a_role_or_a_League()
    {
        var (clubId, manager) = await ClubAsync();
        var leagueId = await LeagueAsync(manager, clubId);
        var stranger = (await (await factory.CreateSignedInClientAsync()).GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!.UserId;
        // Asking isn't in.
        var waiting = await MemberAsync(manager, clubId, "Waiting", approved: false);

        foreach (var userId in new[] { stranger, waiting.UserId })
        {
            Assert.Equal("no-player-in-club", await ProblemTypeAsync(await manager.PutAsync($"/clubs/{clubId}/league-managers/{userId}", null), HttpStatusCode.Conflict));
            Assert.Equal("no-player-in-club", await ProblemTypeAsync(await manager.PutAsync($"/leagues/{leagueId}/managers/{userId}", null), HttpStatusCode.Conflict));
        }

        Assert.Equal(HttpStatusCode.NotFound, (await manager.PutAsync($"/clubs/{clubId}/league-managers/{Guid.NewGuid()}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PutAsync($"/leagues/{leagueId}/managers/{Guid.NewGuid()}", null)).StatusCode);
    }

    [Fact]
    public async Task Losing_the_role_keeps_the_Leagues_and_losing_a_League_keeps_the_role()
    {
        var (clubId, manager) = await ClubAsync();
        var lee = await MemberAsync(manager, clubId, "Lee");
        await RoleAsync(manager, clubId, lee.UserId);
        var own = await LeagueAsync(lee.Client, clubId);

        (await manager.DeleteAsync($"/clubs/{clubId}/league-managers/{lee.UserId}")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await lee.Client.GetAsync($"/leagues/{own}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateAsync(lee.Client, clubId)).StatusCode);

        await RoleAsync(manager, clubId, lee.UserId);
        (await manager.DeleteAsync($"/leagues/{own}/managers/{lee.UserId}")).EnsureSuccessStatusCode();

        Assert.Empty(await LeagueIdsAsync(lee.Client, clubId));
        Assert.Equal(HttpStatusCode.Created, (await CreateAsync(lee.Client, clubId)).StatusCode);
    }

    [Fact]
    public async Task A_manager_outranks_a_League_manager_and_the_role_is_one_row()
    {
        var (clubId, manager) = await ClubAsync();
        var lee = await MemberAsync(manager, clubId, "Lee");
        var admin = await factory.AdminClientAsync();
        async Task<ClubRoleKind?> KindAsync() =>
            (await manager.GetFromJsonAsync<GetClubMembersResponse>($"/clubs/{clubId}/members"))!.Roles.SingleOrDefault(r => r.UserId == lee.UserId)?.Role;

        await RoleAsync(manager, clubId, lee.UserId);
        Assert.Equal(ClubRoleKind.LeagueManager, await KindAsync());
        // Made a manager: the role is raised, and a Club manager can't lower it.
        (await admin.PutAsync($"/clubs/{clubId}/managers/{lee.UserId}", null)).EnsureSuccessStatusCode();
        Assert.Equal(ClubRoleKind.Manager, await KindAsync());
        await RoleAsync(manager, clubId, lee.UserId);
        Assert.Equal(ClubRoleKind.Manager, await KindAsync());
        (await manager.DeleteAsync($"/clubs/{clubId}/league-managers/{lee.UserId}")).EnsureSuccessStatusCode();
        Assert.Equal(ClubRoleKind.Manager, await KindAsync());

        // Taking a Club manager's role takes it, and leaves no lesser one behind.
        (await admin.DeleteAsync($"/clubs/{clubId}/managers/{lee.UserId}")).EnsureSuccessStatusCode();
        Assert.Null(await KindAsync());
    }

    [Fact]
    public async Task Removing_a_manager_leaves_a_League_manager_alone()
    {
        var (clubId, manager) = await ClubAsync();
        var lee = await MemberAsync(manager, clubId, "Lee");
        await RoleAsync(manager, clubId, lee.UserId);

        (await (await factory.AdminClientAsync()).DeleteAsync($"/clubs/{clubId}/managers/{lee.UserId}")).EnsureSuccessStatusCode();

        Assert.True(await ManagesAsync(lee.Client));
    }

    [Fact]
    public async Task A_Leagues_managers_are_named_to_its_Players_and_their_emails_shown_to_the_Clubs_managers_alone()
    {
        var (clubId, manager) = await ClubAsync();
        var leagueId = await LeagueAsync(manager, clubId);
        var ada = await MemberAsync(manager, clubId, "Ada");
        var gone = await MemberAsync(manager, clubId, "Gone");
        await AssignAsync(manager, leagueId, ada.UserId);
        await AssignAsync(manager, leagueId, gone.UserId);
        // They keep the League, and asking to be back isn't being in the Club: they have no Player there to be named by.
        (await gone.Client.DeleteAsync($"/players/{gone.PlayerId}/clubs/{clubId}")).EnsureSuccessStatusCode();
        (await factory.AskToJoinAsync(gone.Client, clubId, gone.PlayerId)).EnsureSuccessStatusCode();
        var pia = await MemberAsync(manager, clubId, "Pia");
        (await manager.PutAsync($"/leagues/{leagueId}/participants/{pia.PlayerId}", null)).EnsureSuccessStatusCode();

        var seenByManager = await LeagueSeenAsync(manager, leagueId);
        Assert.Equal(["Ada", null], seenByManager.Managers.Select(m => m.Name));
        Assert.All(seenByManager.Managers, m => Assert.NotNull(m.Email));
        var seenByAda = await LeagueSeenAsync(ada.Client, leagueId);
        Assert.Equal(["Ada", null], seenByAda.Managers.Select(m => m.Name));
        Assert.All(seenByAda.Managers, m => Assert.Null(m.Email));

        // To its Players: names, and nothing else.
        var ladder = (await pia.Client.GetFromJsonAsync<GetLeagueLadderResponse>($"/leagues/{leagueId}/ladder"))!;
        Assert.Equal(["Ada", null], ladder.Managers.Select(m => m.Name));
        Assert.False(ladder.CanManage);
        Assert.True((await ada.Client.GetFromJsonAsync<GetLeagueLadderResponse>($"/leagues/{leagueId}/ladder"))!.CanManage);
        var raw = await pia.Client.GetStringAsync($"/leagues/{leagueId}/ladder");
        Assert.DoesNotContain("@", raw);
    }

    [Fact]
    public async Task A_League_nobody_was_assigned_to_has_no_managers_of_its_own()
    {
        var (clubId, manager) = await ClubAsync();
        var leagueId = await LeagueAsync(manager, clubId);
        var pia = await MemberAsync(manager, clubId, "Pia");
        (await manager.PutAsync($"/leagues/{leagueId}/participants/{pia.PlayerId}", null)).EnsureSuccessStatusCode();

        Assert.Empty((await pia.Client.GetFromJsonAsync<GetLeagueLadderResponse>($"/leagues/{leagueId}/ladder"))!.Managers);
    }

    [Fact]
    public async Task Who_can_be_added_is_every_member_not_in_it_and_only_a_Club_manager_sees_emails()
    {
        var (clubId, manager) = await ClubAsync();
        var leagueId = await LeagueAsync(manager, clubId);
        var ada = await MemberAsync(manager, clubId, "Ada");
        var pia = await MemberAsync(manager, clubId, "Pia");
        await MemberAsync(manager, clubId, "Waiting", approved: false);
        await AssignAsync(manager, leagueId, ada.UserId);
        (await manager.PutAsync($"/leagues/{leagueId}/participants/{pia.PlayerId}", null)).EnsureSuccessStatusCode();

        var byClubManager = (await manager.GetFromJsonAsync<GetAddablePlayersResponse>($"/leagues/{leagueId}/addable-players"))!;
        var byLeagueManager = (await ada.Client.GetFromJsonAsync<GetAddablePlayersResponse>($"/leagues/{leagueId}/addable-players"))!;

        Assert.Equal([ada.PlayerId], byClubManager.Players.Select(p => p.PlayerId));
        Assert.All(byClubManager.Players, p => Assert.NotNull(p.Email));
        Assert.Equal([("Ada", null)], byLeagueManager.Players.Select(p => (p.Name, p.Email)));
        Assert.Equal(1, byClubManager.Page.Total);
    }

    [Fact]
    public async Task Deleting_a_User_takes_their_Leagues_and_their_role()
    {
        var (clubId, manager) = await ClubAsync();
        var lee = await MemberAsync(manager, clubId, "Lee");
        await RoleAsync(manager, clubId, lee.UserId);
        var leagueId = await LeagueAsync(lee.Client, clubId);

        (await (await factory.AdminClientAsync()).DeleteAsync($"/users/{lee.UserId}")).EnsureSuccessStatusCode();

        Assert.Empty((await LeagueSeenAsync(manager, leagueId)).Managers);
        Assert.DoesNotContain((await manager.GetFromJsonAsync<GetClubMembersResponse>($"/clubs/{clubId}/members"))!.Roles, r => r.UserId == lee.UserId);
    }

    [Fact]
    public async Task The_Admins_list_says_who_is_a_manager_and_who_is_a_League_manager()
    {
        var (clubId, manager) = await ClubAsync();
        var lee = await MemberAsync(manager, clubId, "Lee");
        await RoleAsync(manager, clubId, lee.UserId);
        var me = (await lee.Client.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!;

        var listed = (await (await factory.AdminClientAsync())
            .GetFromJsonAsync<GetUsersResponse>($"/users?q={Uri.EscapeDataString(me.Email)}"))!.Users.Single();

        Assert.Equal((clubId, ClubRoleKind.LeagueManager), (listed.Clubs.Single().ClubId, listed.Clubs.Single().Role));
    }
}
