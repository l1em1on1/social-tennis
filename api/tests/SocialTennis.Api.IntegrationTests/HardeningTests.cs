using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Auth.Contracts;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Features.Leagues.Contracts;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// API hardening II (#109): fixes from the 2026-11-15 review, each with a test
/// that fails without it.
/// </summary>
public class HardeningTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
{
    private static string NewEmail() => $"hardening-{Guid.NewGuid():N}@example.test";

    private async Task<string> RequestTokenAsync(HttpClient client, string email)
    {
        (await client.PostAsJsonAsync("/auth/magic-link", new { email })).EnsureSuccessStatusCode();
        return factory.Sender.TokenFor(email);
    }

    [Fact]
    public async Task Two_redemptions_of_one_link_at_the_same_moment_make_one_session()
    {
        using var client = factory.CreateClient();

        // A few rounds, so the two requests overlap at least once.
        for (var round = 0; round < 5; round++)
        {
            var token = await RequestTokenAsync(client, NewEmail());

            var responses = await Task.WhenAll(
                client.PostAsJsonAsync("/auth/sessions", new { token }),
                client.PostAsJsonAsync("/auth/sessions", new { token }));

            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task Signing_in_with_one_link_makes_the_addresses_other_links_useless()
    {
        using var client = factory.CreateClient();
        var email = NewEmail();
        var older = await RequestTokenAsync(client, email);
        var newer = await RequestTokenAsync(client, email);

        (await client.PostAsJsonAsync("/auth/sessions", new { token = newer })).EnsureSuccessStatusCode();

        var reused = await client.PostAsJsonAsync("/auth/sessions", new { token = older });
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"token":null}""")]
    [InlineData("""{"token":""}""")]
    public async Task Redeeming_without_a_token_is_a_401_not_a_500(string body)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/auth/sessions", new StringContent(body, MediaTypeHeaderValue.Parse("application/json")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_email_with_a_control_character_is_a_400_not_a_500()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/magic-link", new { email = "nul\u0000@example.test" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Sam\u0000")]
    [InlineData("Sam\nSmith")]
    public async Task A_Player_name_with_a_control_character_is_refused(string name)
    {
        using var client = await factory.CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("/players", new { name });

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(problem!.Errors.ContainsKey("name"));
    }

    [Fact]
    public async Task A_Club_name_with_a_control_character_is_refused()
    {
        var admin = await factory.AdminClientAsync();

        var response = await admin.PostAsJsonAsync("/clubs", new { name = "Club\u0000" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_Gender_number_outside_the_list_is_refused()
    {
        using var client = await factory.CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("/players", new { name = "Sam", gender = 7 });

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(problem!.Errors.ContainsKey("gender"));
    }

    [Fact]
    public async Task A_role_in_the_path_works_in_any_case()
    {
        using var user = await factory.CreateSignedInClientAsync();
        var me = await user.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me");
        var admin = await factory.AdminClientAsync();

        var granted = await admin.PutAsync($"/users/{me!.UserId}/roles/manager", null);
        Assert.Equal(HttpStatusCode.NoContent, granted.StatusCode);
        Assert.Contains("Manager", (await user.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!.Roles);

        var revoked = await admin.DeleteAsync($"/users/{me.UserId}/roles/MANAGER");
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.DoesNotContain("Manager", (await user.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!.Roles);
    }

    [Fact]
    public async Task A_Player_removed_from_a_Club_no_longer_sees_its_Leagues()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var admin = await factory.AdminClientAsync();
        var club = await admin.PostAsJsonAsync("/clubs", new { name = $"Hardening Club {Guid.NewGuid():N}" });
        var clubId = (await club.Content.ReadFromJsonAsync<CreateClubResponse>())!.Id;
        var league = await manager.PostAsJsonAsync($"/clubs/{clubId}/leagues", new
        {
            name = "Winter Singles",
            type = "SinglesMixed",
            structure = "Divisional",
            startDate = "2026-12-01",
            endDate = "2027-03-31",
            stageLength = new { count = 1, unit = "Months" },
            stageRoll = "Manual",
            scoreFormat = new { gamesPerSet = 6, setsPerGame = 1, matchTiebreakDecider = false },
        });
        var leagueId = (await league.Content.ReadFromJsonAsync<CreateLeagueResponse>())!.Id;

        using var player = await factory.CreateSignedInClientAsync();
        var created = await player.PostAsJsonAsync("/players", new { name = "Sam" });
        var playerId = (await created.Content.ReadFromJsonAsync<CreatePlayerResponse>())!.Id;
        (await factory.AskToJoinAsync(player, clubId, playerId)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/clubs/{clubId}/members/{playerId}/approval", null)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/leagues/{leagueId}/participants/{playerId}", null)).EnsureSuccessStatusCode();
        var before = await player.GetFromJsonAsync<GetPlayerLeaguesResponse>($"/players/{playerId}/leagues");
        Assert.Contains(before!.Leagues, l => l.Id == leagueId);

        (await manager.DeleteAsync($"/clubs/{clubId}/members/{playerId}")).EnsureSuccessStatusCode();

        var after = await player.GetFromJsonAsync<GetPlayerLeaguesResponse>($"/players/{playerId}/leagues");
        Assert.DoesNotContain(after!.Leagues, l => l.Id == leagueId);
    }

    [Fact]
    public async Task A_login_can_add_at_most_ten_Players()
    {
        using var client = await factory.CreateSignedInClientAsync();
        for (var i = 1; i <= Player.MaxPerUser; i++)
        {
            (await client.PostAsJsonAsync("/players", new { name = $"Player {i}" })).EnsureSuccessStatusCode();
        }

        var eleventh = await client.PostAsJsonAsync("/players", new { name = "One too many" });

        Assert.Equal(HttpStatusCode.Conflict, eleventh.StatusCode);
        var problem = await eleventh.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("player-limit", problem!.Type);
        var players = await client.GetFromJsonAsync<GetPlayersResponse>("/players");
        Assert.Equal(Player.MaxPerUser, players!.Players.Count);
    }

    [Fact]
    public async Task Players_a_Manager_added_count_towards_the_limit_and_a_Manager_can_still_add_more()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await factory.AClubIdAsync();
        var email = NewEmail();
        for (var i = 1; i <= Player.MaxPerUser; i++)
        {
            (await manager.PostAsJsonAsync($"/clubs/{clubId}/players", new { name = $"Junior {i}", email }))
                .EnsureSuccessStatusCode();
        }

        using var parent = await factory.CreateSignedInClientAsync(email);
        var ownAdd = await parent.PostAsJsonAsync("/players", new { name = "One more" });

        Assert.Equal(HttpStatusCode.Conflict, ownAdd.StatusCode);
        Assert.Equal("player-limit", (await ownAdd.Content.ReadFromJsonAsync<ProblemDetails>())!.Type);

        var managerAdd = await manager.PostAsJsonAsync($"/clubs/{clubId}/players", new { name = "Junior 11", email });
        Assert.Equal(HttpStatusCode.Created, managerAdd.StatusCode);
        var players = await parent.GetFromJsonAsync<GetPlayersResponse>("/players");
        Assert.Equal(Player.MaxPerUser + 1, players!.Players.Count);
    }

    [Fact]
    public async Task A_session_stops_working_when_it_expires()
    {
        var clock = new MovableClock(DateTimeOffset.UtcNow);
        using var timed = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using var client = timed.CreateClient();
        var email = NewEmail();
        (await client.PostAsJsonAsync("/auth/magic-link", new { email })).EnsureSuccessStatusCode();
        var redeemed = await client.PostAsJsonAsync("/auth/sessions", new { token = factory.Sender.TokenFor(email) });
        var session = await redeemed.Content.ReadFromJsonAsync<RedeemMagicLinkResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/auth/me")).StatusCode);

        clock.Now = session.ExpiresAt.AddSeconds(1);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);
    }

    /// <summary>A clock the test moves by hand (ADR-0016: time comes from the injected TimeProvider).</summary>
    private sealed class MovableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
