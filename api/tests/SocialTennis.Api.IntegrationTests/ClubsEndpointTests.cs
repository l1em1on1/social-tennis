using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Clubs.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// The project's one testing seam (see issue #1, Testing Decisions): drive the
/// API over its HTTP boundary against a real Postgres — the compose `postgres`
/// service — never mocked repositories. The connection string arrives via
/// ConnectionStrings__Default from docker-compose's api-tests service.
/// </summary>
/// <remarks>
/// The database is shared with every other test class and outlives a run, so
/// these tests find their own Club among whatever else is there.
/// </remarks>
public class ClubsEndpointTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
{
    private static async Task<IReadOnlyList<ClubSummary>> ClubsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<GetClubsResponse>("/clubs"))!.Clubs;

    /// <summary>
    /// Asked of the model, not the database: the migration that removed the
    /// old seed keeps that Club where it already has members, and this
    /// database is shared and outlives a run.
    /// </summary>
    [Fact]
    public void The_model_seeds_no_Club()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();

        var club = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Club))!;

        Assert.Empty(club.GetSeedData());
    }

    [Fact]
    public async Task An_Admin_creates_a_Club_and_everyone_signed_in_sees_it()
    {
        var admin = await factory.AdminClientAsync();
        using var player = await factory.CreateSignedInClientAsync();
        var name = $"Riverside {Guid.NewGuid():N}";

        var response = await admin.PostAsJsonAsync("/clubs", new { name = $"  {name} " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreateClubResponse>();
        Assert.Equal(name, created!.Name);
        Assert.Contains(await ClubsAsync(player), club => club.Id == created.Id && club.Name == name);
    }

    [Fact]
    public async Task An_Admin_renames_a_Club_and_its_members_stay()
    {
        var admin = await factory.AdminClientAsync();
        using var player = await factory.CreateSignedInClientAsync();
        var created = await (await admin.PostAsJsonAsync("/clubs", new { name = $"Old {Guid.NewGuid():N}" }))
            .Content.ReadFromJsonAsync<CreateClubResponse>();
        var playerId = (await (await player.PostAsJsonAsync("/players", new { name = "Sam" }))
            .Content.ReadFromJsonAsync<Features.Players.Contracts.CreatePlayerResponse>())!.Id;
        await player.PutAsync($"/clubs/{created!.Id}/members/{playerId}", null);
        var name = $"New {Guid.NewGuid():N}";

        var response = await admin.PutAsJsonAsync($"/clubs/{created.Id}", new { name });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(name, (await ClubsAsync(player)).Single(club => club.Id == created.Id).Name);
        var members = await admin.GetFromJsonAsync<GetClubMembersResponse>($"/clubs/{created.Id}/members");
        Assert.Single(members!.Members, member => member.PlayerId == playerId);
    }

    [Fact]
    public async Task Renaming_a_Club_that_does_not_exist_is_not_found()
    {
        var admin = await factory.AdminClientAsync();

        var response = await admin.PutAsJsonAsync($"/clubs/{Guid.NewGuid()}", new { name = "Nowhere" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_Club_needs_a_name(string name)
    {
        var admin = await factory.AdminClientAsync();
        var clubId = await factory.AClubIdAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/clubs", new { name })).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await admin.PutAsJsonAsync($"/clubs/{clubId}", new { name })).StatusCode);
    }

    [Fact]
    public async Task A_name_longer_than_200_characters_is_refused()
    {
        var admin = await factory.AdminClientAsync();

        var response = await admin.PostAsJsonAsync("/clubs", new { name = new string('a', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Only_an_Admin_creates_or_renames_Clubs_not_a_Manager_or_a_Player()
    {
        using var player = await factory.CreateSignedInClientAsync();
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await factory.AClubIdAsync();

        foreach (var client in new[] { player, manager })
        {
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.PostAsJsonAsync("/clubs", new { name = "Mine" })).StatusCode);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.PutAsJsonAsync($"/clubs/{clubId}", new { name = "Mine" })).StatusCode);
        }
    }

    [Fact]
    public async Task GetClubs_reports_the_total_matching_the_query()
    {
        using var client = await factory.CreateSignedInClientAsync();

        var body = await client.GetFromJsonAsync<GetClubsResponse>("/clubs");

        Assert.NotNull(body);
        Assert.Equal(body.Clubs.Count, body.Page.Total);
    }

    [Fact]
    public async Task GetClubs_is_refused_without_a_session()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/clubs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
