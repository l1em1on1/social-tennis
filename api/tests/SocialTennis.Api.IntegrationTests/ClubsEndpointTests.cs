using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// The project's one testing seam (see issue #1, Testing Decisions): drive the
/// API over its HTTP boundary against a real Postgres — the compose `postgres`
/// service — never mocked repositories. The connection string arrives via
/// ConnectionStrings__Default from docker-compose's api-tests service.
/// </summary>
public class ClubsEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetClubs_returns_the_seeded_club_from_postgres()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/clubs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var clubs = await response.Content.ReadFromJsonAsync<List<Club>>();
        Assert.NotNull(clubs);
        var club = Assert.Single(clubs);
        Assert.Equal("Social Tennis Club", club.Name);
    }

    [Fact]
    public async Task OpenApi_document_is_served()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"/clubs\"", doc);
    }
}
