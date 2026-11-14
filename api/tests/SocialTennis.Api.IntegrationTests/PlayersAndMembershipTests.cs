using System.Net;
using System.Net.Http.Json;
using SocialTennis.Api.Features.Auth.Contracts;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// A User's own Players and their Club membership, over HTTP against the real
/// Postgres. Every test signs in as a fresh User, so they don't see each other.
/// </summary>
public class PlayersAndMembershipTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
{
    private static async Task<CreatePlayerResponse> CreatePlayerAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/players", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatePlayerResponse>())!;
    }

    private static async Task<IReadOnlyList<PlayerSummary>> PlayersAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<GetPlayersResponse>("/players"))!.Players;

    private static async Task<GetClubMembersResponse> MembersAsync(HttpClient manager, Guid clubId) =>
        (await manager.GetFromJsonAsync<GetClubMembersResponse>($"/clubs/{clubId}/members"))!;

    private static async Task<Guid> AnyClubIdAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<GetClubsResponse>("/clubs"))!.Clubs[0].Id;

    [Fact]
    public async Task A_new_User_holds_no_Player()
    {
        using var client = await factory.CreateSignedInClientAsync();

        var body = await client.GetFromJsonAsync<GetPlayersResponse>("/players");

        Assert.Empty(body!.Players);
        Assert.Equal(0, body.Page.Total);
    }

    [Fact]
    public async Task A_User_can_hold_several_Players_and_sees_them_oldest_first()
    {
        using var client = await factory.CreateSignedInClientAsync();

        await CreatePlayerAsync(client, "  Pat Parent  ");
        await CreatePlayerAsync(client, "Alex Junior");

        var players = await PlayersAsync(client);
        Assert.Equal(["Pat Parent", "Alex Junior"], players.Select(p => p.Name));
        Assert.All(players, p => Assert.Empty(p.Clubs));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_Player_needs_a_name(string name)
    {
        using var client = await factory.CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("/players", new { name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await PlayersAsync(client));
    }

    [Fact]
    public async Task A_name_longer_than_100_characters_is_refused()
    {
        using var client = await factory.CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("/players", new { name = new string('a', 101) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_holding_User_can_rename_a_Player()
    {
        using var client = await factory.CreateSignedInClientAsync();
        var player = await CreatePlayerAsync(client, "Sam");

        var response = await client.PutAsJsonAsync($"/players/{player.Id}", new { name = "Sam Smith" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Sam Smith", Assert.Single(await PlayersAsync(client)).Name);
    }

    [Fact]
    public async Task Another_Users_Player_cannot_be_seen_renamed_or_joined_to_a_Club()
    {
        using var owner = await factory.CreateSignedInClientAsync();
        using var other = await factory.CreateSignedInClientAsync();
        var player = await CreatePlayerAsync(owner, "Sam");
        var clubId = await AnyClubIdAsync(other);

        var rename = await other.PutAsJsonAsync($"/players/{player.Id}", new { name = "Hijacked" });
        var join = await other.PutAsync($"/clubs/{clubId}/members/{player.Id}", null);

        Assert.Equal(HttpStatusCode.NotFound, rename.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, join.StatusCode);
        Assert.Empty(await PlayersAsync(other));
        var unchanged = Assert.Single(await PlayersAsync(owner));
        Assert.Equal("Sam", unchanged.Name);
        Assert.Empty(unchanged.Clubs);
    }

    [Fact]
    public async Task Asking_to_join_waits_for_a_Manager_and_approval_makes_the_Player_a_member()
    {
        using var client = await factory.CreateSignedInClientAsync();
        using var manager = await factory.CreateManagerClientAsync();
        var player = await CreatePlayerAsync(client, "Sam");
        var sibling = await CreatePlayerAsync(client, "Alex");
        var clubId = await AnyClubIdAsync(client);

        var ask = await client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null);

        Assert.Equal(HttpStatusCode.NoContent, ask.StatusCode);
        var waiting = Assert.Single((await PlayersAsync(client)).Single(p => p.Id == player.Id).Clubs);
        Assert.False(waiting.Approved);
        var request = (await MembersAsync(manager, clubId)).Members.Single(m => m.PlayerId == player.Id);
        Assert.False(request.Approved);
        Assert.Equal("Sam", request.Name);

        var approve = await manager.PutAsync($"/clubs/{clubId}/members/{player.Id}/approval", null);

        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);
        var players = await PlayersAsync(client);
        Assert.True(Assert.Single(players.Single(p => p.Id == player.Id).Clubs).Approved);
        Assert.Empty(players.Single(p => p.Id == sibling.Id).Clubs);
        Assert.True((await MembersAsync(manager, clubId)).Members.Single(m => m.PlayerId == player.Id).Approved);
    }

    [Fact]
    public async Task Asking_again_changes_nothing_and_never_undoes_an_approval()
    {
        using var client = await factory.CreateSignedInClientAsync();
        using var manager = await factory.CreateManagerClientAsync();
        var player = await CreatePlayerAsync(client, "Sam");
        var clubId = await AnyClubIdAsync(client);

        var presses = await Task.WhenAll(
            client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null),
            client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null),
            client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null));
        Assert.All(presses, press => Assert.Equal(HttpStatusCode.NoContent, press.StatusCode));
        Assert.False(Assert.Single(Assert.Single(await PlayersAsync(client)).Clubs).Approved);

        await manager.PutAsync($"/clubs/{clubId}/members/{player.Id}/approval", null);
        await client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null);

        Assert.True(Assert.Single(Assert.Single(await PlayersAsync(client)).Clubs).Approved);
    }

    [Fact]
    public async Task A_Manager_can_decline_a_request_and_the_Player_can_ask_again()
    {
        using var client = await factory.CreateSignedInClientAsync();
        using var manager = await factory.CreateManagerClientAsync();
        var player = await CreatePlayerAsync(client, "Sam");
        var clubId = await AnyClubIdAsync(client);
        await client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null);

        var decline = await manager.DeleteAsync($"/clubs/{clubId}/members/{player.Id}");

        Assert.Equal(HttpStatusCode.NoContent, decline.StatusCode);
        Assert.Empty(Assert.Single(await PlayersAsync(client)).Clubs);
        Assert.DoesNotContain((await MembersAsync(manager, clubId)).Members, m => m.PlayerId == player.Id);

        await client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null);
        Assert.False(Assert.Single(Assert.Single(await PlayersAsync(client)).Clubs).Approved);
    }

    [Fact]
    public async Task An_invited_email_is_in_the_Club_as_soon_as_its_User_adds_a_Player()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var typed = $"Invited-{Guid.NewGuid():N}@Example.test";
        var email = typed.ToLowerInvariant();
        var clubId = await AnyClubIdAsync(manager);

        var invite = await manager.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email = $"  {typed} " });
        var again = await manager.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email });

        Assert.Equal(HttpStatusCode.NoContent, invite.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        var pending = Assert.Single((await MembersAsync(manager, clubId)).Invites, i => i.Email == email);
        Assert.False(pending.Joined);

        using var invited = await factory.CreateSignedInClientAsync(email);
        await CreatePlayerAsync(invited, "Pat");
        await CreatePlayerAsync(invited, "Pat Junior");

        Assert.All(await PlayersAsync(invited), p => Assert.True(Assert.Single(p.Clubs).Approved));
        Assert.True((await MembersAsync(manager, clubId)).Invites.Single(i => i.Email == email).Joined);
    }

    [Fact]
    public async Task Inviting_an_email_whose_User_already_has_Players_lets_them_in_including_a_waiting_request()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var email = $"existing-{Guid.NewGuid():N}@example.test";
        using var client = await factory.CreateSignedInClientAsync(email);
        var asked = await CreatePlayerAsync(client, "Sam");
        await CreatePlayerAsync(client, "Alex");
        var clubId = await AnyClubIdAsync(client);
        await client.PutAsync($"/clubs/{clubId}/members/{asked.Id}", null);

        var invite = await manager.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email });

        Assert.Equal(HttpStatusCode.NoContent, invite.StatusCode);
        Assert.All(await PlayersAsync(client), p => Assert.True(Assert.Single(p.Clubs).Approved));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task An_invite_needs_an_email_address(string email)
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await AnyClubIdAsync(manager);

        var response = await manager.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Only_a_Manager_or_Admin_can_see_approve_remove_or_invite()
    {
        using var client = await factory.CreateSignedInClientAsync();
        var player = await CreatePlayerAsync(client, "Sam");
        var clubId = await AnyClubIdAsync(client);
        await client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/clubs/{clubId}/members")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PutAsync($"/clubs/{clubId}/members/{player.Id}/approval", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/clubs/{clubId}/members/{player.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email = "friend@example.test" })).StatusCode);
        Assert.False(Assert.Single(Assert.Single(await PlayersAsync(client)).Clubs).Approved);
    }

    [Fact]
    public async Task An_email_listed_in_configuration_is_an_Admin_and_can_do_what_a_Manager_can()
    {
        using var admin = await factory.CreateSignedInClientAsync(AuthTestFactory.AdminEmail);
        using var player = await factory.CreateSignedInClientAsync();
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await AnyClubIdAsync(admin);

        var adminRoles = (await admin.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!.Roles;
        var managerRoles = (await manager.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!.Roles;
        var playerRoles = (await player.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!.Roles;

        Assert.Equal(["Admin", "Manager"], adminRoles.Order());
        Assert.Equal(["Manager"], managerRoles);
        Assert.Empty(playerRoles);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/clubs/{clubId}/members")).StatusCode);
    }

    [Fact]
    public async Task Joining_a_Club_that_does_not_exist_is_not_found()
    {
        using var client = await factory.CreateSignedInClientAsync();
        var player = await CreatePlayerAsync(client, "Sam");

        var response = await client.PutAsync($"/clubs/{Guid.NewGuid()}/members/{player.Id}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Players_are_refused_without_a_session()
    {
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/players")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/players", new { name = "Sam" })).StatusCode);
    }
}
