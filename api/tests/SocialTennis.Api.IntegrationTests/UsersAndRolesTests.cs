using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Auth.Contracts;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Features.Leagues.Contracts;
using SocialTennis.Api.Features.Players.Contracts;
using SocialTennis.Api.Features.Users.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// An Admin granting and removing roles, over HTTP against the real Postgres.
/// The rule for the very last Admin is in the unit tests (AdminRoleRulesTests):
/// it depends on there being no other Admin in a database every test shares.
/// </summary>
public class UsersAndRolesTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
{
    private static async Task<GetCurrentUserResponse> MeAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!;

    /// <summary>
    /// Finds a User by paging through the whole list: the database every test
    /// shares holds more Users than one page (#134).
    /// </summary>
    private async Task<UserSummary> UserAsync(Guid userId)
    {
        var admin = await factory.AdminClientAsync();
        for (var offset = 0; ; offset += 100)
        {
            var list = (await admin.GetFromJsonAsync<GetUsersResponse>($"/users?offset={offset}&limit=100"))!;
            var found = list.Users.SingleOrDefault(user => user.Id == userId);
            if (found is not null)
            {
                return found;
            }
            if (offset + list.Users.Count >= list.Page.Total)
            {
                throw new InvalidOperationException($"User {userId} isn't listed.");
            }
        }
    }

    private static async Task<GetUsersResponse> SearchAsync(HttpClient admin, string query) =>
        (await admin.GetFromJsonAsync<GetUsersResponse>($"/users?q={Uri.EscapeDataString(query)}"))!;

    [Fact]
    public async Task The_configured_Admin_exists_with_no_database_step_and_is_listed_as_such()
    {
        var admin = await factory.AdminClientAsync();

        var me = await MeAsync(admin);
        var listed = await UserAsync(me.UserId);

        // An Admin manages every Club, with no role in any.
        Assert.True(me is { Admin: true, Manages: true });
        Assert.Empty(listed.Clubs);
        Assert.True(listed.Admin);
        Assert.True(listed.AdminBySetting);
        Assert.False(listed.LastAdmin);
    }

    [Fact]
    public async Task An_Admin_sees_every_User_with_their_Players_and_roles()
    {
        using var client = await factory.CreateSignedInClientAsync();
        await client.PostAsJsonAsync("/players", new { name = "Pat Parent" });
        await client.PostAsJsonAsync("/players", new { name = "Alex Junior" });
        var me = await MeAsync(client);

        var listed = await UserAsync(me.UserId);

        Assert.Equal(me.Email, listed.Email);
        Assert.Equal(["Pat Parent", "Alex Junior"], listed.Players.Select(player => player.Name));
        Assert.Empty(listed.Clubs);
        Assert.False(listed.Admin);
    }

    [Fact]
    public async Task Search_finds_a_User_by_part_of_their_email_or_of_a_Player_name_in_any_case()
    {
        var admin = await factory.AdminClientAsync();
        var token = Guid.NewGuid().ToString("N")[..12];
        using var client = await factory.CreateSignedInClientAsync($"searchable-{token}@example.test");
        await client.PostAsJsonAsync("/players", new { name = $"Quinn {token}" });
        var me = await MeAsync(client);

        var byEmail = await SearchAsync(admin, $"SEARCHABLE-{token}");
        var byName = await SearchAsync(admin, $"quinn {token}".ToUpperInvariant());

        Assert.Equal(me.UserId, Assert.Single(byEmail.Users).Id);
        Assert.Equal(me.UserId, Assert.Single(byName.Users).Id);
        Assert.Equal((1, 0, 50), (byName.Page.Total, byName.Page.Offset, byName.Page.Limit));
    }

    [Fact]
    public async Task A_search_for_a_whole_email_lists_that_User_first()
    {
        var admin = await factory.AdminClientAsync();
        var email = $"exact-{Guid.NewGuid():N}@example.test";
        using var exact = await factory.CreateSignedInClientAsync(email);
        using var newer = await factory.CreateSignedInClientAsync($"x-{email}");

        var list = await SearchAsync(admin, email.ToUpperInvariant());

        Assert.Equal([email, $"x-{email}"], list.Users.Select(user => user.Email));
    }

    [Fact]
    public async Task A_search_is_text_not_a_pattern()
    {
        var admin = await factory.AdminClientAsync();

        // % and _ match themselves, and no email or Player name here contains them.
        var list = await SearchAsync(admin, "%_");

        Assert.Empty(list.Users);
        Assert.Equal(0, list.Page.Total);
    }

    [Fact]
    public async Task Users_of_a_Club_are_those_with_a_Player_there_or_asking_or_a_role_or_League_there()
    {
        var token = Guid.NewGuid().ToString("N");
        var admin = await factory.AdminClientAsync();
        async Task<Guid> ClubAsync() =>
            (await (await admin.PostAsJsonAsync("/clubs", new { name = $"Club {Guid.NewGuid():N}" }))
                .Content.ReadFromJsonAsync<CreateClubResponse>())!.Id;
        var (here, elsewhere) = (await ClubAsync(), await ClubAsync());
        async Task<(HttpClient Client, Guid UserId, Guid PlayerId, string Email)> PersonAsync(string kind)
        {
            var email = $"{kind}-{token}@example.test";
            var client = await factory.CreateSignedInClientAsync(email);
            var created = await client.PostAsJsonAsync("/players", new { name = kind });
            return (client, (await MeAsync(client)).UserId, (await created.Content.ReadFromJsonAsync<CreatePlayerResponse>())!.Id, email);
        }

        var manager = await factory.CreateManagerClientAsync(here);
        var managerEmail = (await MeAsync(manager)).Email;
        var member = await PersonAsync("member");
        (await factory.AskToJoinAsync(member.Client, here, member.PlayerId)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/clubs/{here}/members/{member.PlayerId}/approval", null)).EnsureSuccessStatusCode();
        var asking = await PersonAsync("asking");
        (await factory.AskToJoinAsync(asking.Client, here, asking.PlayerId)).EnsureSuccessStatusCode();
        var roleHolder = await PersonAsync("rolelee");
        (await factory.AskToJoinAsync(roleHolder.Client, here, roleHolder.PlayerId)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/clubs/{here}/members/{roleHolder.PlayerId}/approval", null)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/clubs/{here}/league-managers/{roleHolder.UserId}", null)).EnsureSuccessStatusCode();
        (await roleHolder.Client.DeleteAsync($"/players/{roleHolder.PlayerId}/clubs/{here}")).EnsureSuccessStatusCode();
        var runner = await PersonAsync("runner");
        (await factory.AskToJoinAsync(runner.Client, here, runner.PlayerId)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/clubs/{here}/members/{runner.PlayerId}/approval", null)).EnsureSuccessStatusCode();
        var league = await manager.PostAsJsonAsync($"/clubs/{here}/leagues", new
        {
            name = "Filter League",
            type = "SinglesMixed",
            structure = "Divisional",
            startDate = "2030-12-01",
            endDate = "2031-03-31",
            stageLength = new { count = 1, unit = "Months" },
            stageRoll = "Manual",
            scoreFormat = new { gamesPerSet = 6, setsPerGame = 1, matchTiebreakDecider = false },
        });
        var leagueId = (await league.Content.ReadFromJsonAsync<CreateLeagueResponse>())!.Id;
        (await manager.PutAsync($"/leagues/{leagueId}/managers/{runner.UserId}", null)).EnsureSuccessStatusCode();
        (await runner.Client.DeleteAsync($"/players/{runner.PlayerId}/clubs/{here}")).EnsureSuccessStatusCode();
        // In another Club, and in none.
        var other = await PersonAsync("other");
        (await factory.AskToJoinAsync(other.Client, elsewhere, other.PlayerId)).EnsureSuccessStatusCode();
        var nobody = await PersonAsync("nobody");

        async Task<IEnumerable<string>> EmailsAsync(string query) =>
            (await admin.GetFromJsonAsync<GetUsersResponse>($"/users?limit=100&{query}"))!.Users.Select(u => u.Email);

        Assert.Equal(
            new[] { member.Email, asking.Email, roleHolder.Email, runner.Email }.Order(),
            (await EmailsAsync($"clubId={here}&q={token}")).Order());
        Assert.Contains(managerEmail, await EmailsAsync($"clubId={here}"));
        Assert.Equal([other.Email], await EmailsAsync($"clubId={elsewhere}&q={token}"));
        // With the search and paging as ever, and everyone without it.
        var first = (await admin.GetFromJsonAsync<GetUsersResponse>($"/users?clubId={here}&q={token}&limit=1"))!;
        Assert.Equal((1, 4), (first.Users.Count, first.Page.Total));
        Assert.Equal(6, (await EmailsAsync($"q={token}")).Count());
        // A Club that isn't there: an empty list, and no error.
        Assert.Empty(await EmailsAsync($"clubId={Guid.NewGuid()}"));
    }

    [Fact]
    public async Task Users_come_newest_first_one_page_at_a_time()
    {
        var admin = await factory.AdminClientAsync();
        var token = Guid.NewGuid().ToString("N")[..12];
        var ids = new List<Guid>();
        foreach (var n in new[] { 1, 2, 3 })
        {
            using var client = await factory.CreateSignedInClientAsync();
            await client.PostAsJsonAsync("/players", new { name = $"Pager {token} {n}" });
            ids.Add((await MeAsync(client)).UserId);
        }

        var first = (await admin.GetFromJsonAsync<GetUsersResponse>($"/users?q={token}&limit=2"))!;
        var second = (await admin.GetFromJsonAsync<GetUsersResponse>($"/users?q={token}&offset=2&limit=2"))!;

        Assert.Equal([ids[2], ids[1]], first.Users.Select(user => user.Id));
        Assert.Equal((3, 0, 2), (first.Page.Total, first.Page.Offset, first.Page.Limit));
        Assert.Equal([ids[0]], second.Users.Select(user => user.Id));
        Assert.Equal((3, 2, 2), (second.Page.Total, second.Page.Offset, second.Page.Limit));
    }

    [Theory]
    [InlineData("offset=-1", "offset")]
    [InlineData("limit=0", "limit")]
    [InlineData("limit=101", "limit")]
    [InlineData("q=%00", "q")]
    public async Task Paging_or_search_out_of_range_is_refused(string query, string field)
    {
        var admin = await factory.AdminClientAsync();

        var response = await admin.GetAsync($"/users?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.True(problem!.Errors.ContainsKey(field));
    }

    [Fact]
    public async Task Making_and_removing_a_Clubs_manager_takes_effect_on_what_that_User_can_do_at_once()
    {
        var admin = await factory.AdminClientAsync();
        using var client = await factory.CreateSignedInClientAsync();
        var me = await MeAsync(client);
        var clubId = await factory.AClubIdAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/clubs/{clubId}/members")).StatusCode);

        var grant = await admin.PutAsync($"/clubs/{clubId}/managers/{me.UserId}", null);
        var again = await admin.PutAsync($"/clubs/{clubId}/managers/{me.UserId}", null);

        // The same session, no new sign-in.
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/clubs/{clubId}/members")).StatusCode);
        Assert.True(await MeAsync(client) is { Admin: false, Manages: true });
        var role = Assert.Single((await UserAsync(me.UserId)).Clubs);
        Assert.Equal((clubId, ClubRoleKind.Manager), (role.ClubId, role.Role));
        // A Manager is not an Admin.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/users")).StatusCode);

        var revoke = await admin.DeleteAsync($"/clubs/{clubId}/managers/{me.UserId}");
        var revokedAgain = await admin.DeleteAsync($"/clubs/{clubId}/managers/{me.UserId}");

        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, revokedAgain.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/clubs/{clubId}/members")).StatusCode);
        Assert.False((await MeAsync(client)).Manages);
        Assert.Empty((await UserAsync(me.UserId)).Clubs);
    }

    [Fact]
    public async Task An_Admin_can_make_another_User_an_Admin_and_take_it_back()
    {
        var admin = await factory.AdminClientAsync();
        using var client = await factory.CreateSignedInClientAsync();
        var me = await MeAsync(client);

        await admin.PutAsync($"/users/{me.UserId}/roles/Admin", null);

        Assert.True(await MeAsync(client) is { Admin: true, Manages: true });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/users")).StatusCode);
        var listed = await UserAsync(me.UserId);
        Assert.True(listed.Admin);
        Assert.False(listed.AdminBySetting);

        var revoke = await admin.DeleteAsync($"/users/{me.UserId}/roles/Admin");

        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/users")).StatusCode);
    }

    [Fact]
    public async Task The_Admin_role_cannot_be_removed_from_an_Admin_by_configuration()
    {
        var admin = await factory.AdminClientAsync();
        var me = await MeAsync(admin);

        var response = await admin.DeleteAsync($"/users/{me.UserId}/roles/Admin");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("configuration", problem!.Detail);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/users")).StatusCode);
    }

    [Fact]
    public async Task A_role_that_does_not_exist_or_a_User_that_does_not_is_not_found()
    {
        var admin = await factory.AdminClientAsync();
        var me = await MeAsync(admin);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync($"/users/{me.UserId}/roles/Owner", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync($"/users/{me.UserId}/roles/3", null)).StatusCode);
        // Managers belong to a Club: there is no such role everywhere (#110).
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync($"/users/{me.UserId}/roles/Manager", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync($"/users/{Guid.NewGuid()}/roles/Admin", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/users/{Guid.NewGuid()}/roles/Admin")).StatusCode);

        var clubId = await factory.AClubIdAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync($"/clubs/{clubId}/managers/{Guid.NewGuid()}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/clubs/{clubId}/managers/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync($"/clubs/{Guid.NewGuid()}/managers/{me.UserId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/clubs/{Guid.NewGuid()}/managers/{me.UserId}")).StatusCode);
    }

    [Fact]
    public async Task Only_an_Admin_can_see_Users_or_change_roles_not_a_Manager_or_a_Player()
    {
        using var player = await factory.CreateSignedInClientAsync();
        using var manager = await factory.CreateManagerClientAsync();
        var target = (await MeAsync(player)).UserId;
        var clubId = await factory.AClubIdAsync();

        foreach (var client in new[] { player, manager })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/users")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsync($"/users/{target}/roles/Admin", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/users/{target}/roles/Admin")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsync($"/clubs/{clubId}/managers/{target}", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/clubs/{clubId}/managers/{target}")).StatusCode);
        }

        Assert.True(await MeAsync(player) is { Admin: false, Manages: false });
    }
}
