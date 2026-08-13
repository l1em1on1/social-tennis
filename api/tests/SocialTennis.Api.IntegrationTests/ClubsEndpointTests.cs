using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SocialTennis.Api.Features.Clubs.Contracts;

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
        var body = await response.Content.ReadFromJsonAsync<GetClubsResponse>();
        Assert.NotNull(body);
        var club = Assert.Single(body.Clubs);
        Assert.Equal("Social Tennis Club", club.Name);
    }

    [Fact]
    public async Task GetClubs_reports_the_total_matching_the_query()
    {
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<GetClubsResponse>("/clubs");

        Assert.NotNull(body);
        Assert.Equal(body.Clubs.Count, body.Page.Total);
    }
}
