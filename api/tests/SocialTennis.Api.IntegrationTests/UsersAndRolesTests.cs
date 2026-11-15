using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SocialTennis.Api.Features.Auth.Contracts;
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

    private async Task<UserSummary> UserAsync(Guid userId) =>
        (await (await factory.AdminClientAsync()).GetFromJsonAsync<GetUsersResponse>("/users"))!
            .Users.Single(user => user.Id == userId);

    [Fact]
    public async Task The_configured_Admin_exists_with_no_database_step_and_is_listed_as_such()
    {
        var admin = await factory.AdminClientAsync();

        var me = await MeAsync(admin);
        var listed = await UserAsync(me.UserId);

        Assert.Equal(["Admin", "Manager"], me.Roles.Order());
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
        Assert.False(listed.Manager);
        Assert.False(listed.Admin);
    }

    [Fact]
    public async Task Granting_and_removing_Manager_takes_effect_on_what_that_User_can_do_at_once()
    {
        var admin = await factory.AdminClientAsync();
        using var client = await factory.CreateSignedInClientAsync();
        var me = await MeAsync(client);
        var clubId = await factory.AClubIdAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/clubs/{clubId}/members")).StatusCode);

        var grant = await admin.PutAsync($"/users/{me.UserId}/roles/Manager", null);
        var again = await admin.PutAsync($"/users/{me.UserId}/roles/Manager", null);

        // The same session, no new sign-in.
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/clubs/{clubId}/members")).StatusCode);
        Assert.Equal(["Manager"], (await MeAsync(client)).Roles);
        Assert.True((await UserAsync(me.UserId)).Manager);
        // A Manager is not an Admin.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/users")).StatusCode);

        var revoke = await admin.DeleteAsync($"/users/{me.UserId}/roles/Manager");

        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/clubs/{clubId}/members")).StatusCode);
        Assert.Empty((await MeAsync(client)).Roles);
    }

    [Fact]
    public async Task An_Admin_can_make_another_User_an_Admin_and_take_it_back()
    {
        var admin = await factory.AdminClientAsync();
        using var client = await factory.CreateSignedInClientAsync();
        var me = await MeAsync(client);

        await admin.PutAsync($"/users/{me.UserId}/roles/Admin", null);

        Assert.Equal(["Admin", "Manager"], (await MeAsync(client)).Roles.Order());
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
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync($"/users/{Guid.NewGuid()}/roles/Manager", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/users/{Guid.NewGuid()}/roles/Manager")).StatusCode);
    }

    [Fact]
    public async Task Only_an_Admin_can_see_Users_or_change_roles_not_a_Manager_or_a_Player()
    {
        using var player = await factory.CreateSignedInClientAsync();
        using var manager = await factory.CreateManagerClientAsync();
        var target = (await MeAsync(player)).UserId;

        foreach (var client in new[] { player, manager })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/users")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsync($"/users/{target}/roles/Admin", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/users/{target}/roles/Manager")).StatusCode);
        }

        Assert.Empty((await MeAsync(player)).Roles);
    }
}
