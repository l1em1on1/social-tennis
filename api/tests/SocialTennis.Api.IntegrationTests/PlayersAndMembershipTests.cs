using System.Net;
using System.Net.Http.Json;
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
    public async Task A_Player_joins_a_Club_and_then_appears_as_a_member()
    {
        using var client = await factory.CreateSignedInClientAsync();
        var player = await CreatePlayerAsync(client, "Sam");
        var sibling = await CreatePlayerAsync(client, "Alex");
        var clubId = await AnyClubIdAsync(client);

        var response = await client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var players = await PlayersAsync(client);
        Assert.Equal(clubId, Assert.Single(players.Single(p => p.Id == player.Id).Clubs).Id);
        Assert.Empty(players.Single(p => p.Id == sibling.Id).Clubs);
    }

    [Fact]
    public async Task Joining_twice_is_the_same_as_joining_once()
    {
        using var client = await factory.CreateSignedInClientAsync();
        var player = await CreatePlayerAsync(client, "Sam");
        var clubId = await AnyClubIdAsync(client);

        var presses = await Task.WhenAll(
            client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null),
            client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null),
            client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null));

        Assert.All(presses, press => Assert.Equal(HttpStatusCode.NoContent, press.StatusCode));
        Assert.Single(Assert.Single(await PlayersAsync(client)).Clubs);
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
