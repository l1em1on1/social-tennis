using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using SocialTennis.Api.Features.Auth.Contracts;

namespace SocialTennis.Api.IntegrationTests;

public class AuthFlowTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
{
    private static string UniqueEmail() => $"player-{Guid.NewGuid():N}@example.test";

    private async Task<string> RequestTokenAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/auth/magic-link", new { email });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return factory.Sender.TokenFor(email);
    }

    private static async Task<SessionResponse> RedeemAsync(HttpClient client, string token)
    {
        var response = await client.PostAsJsonAsync("/auth/sessions", new { token });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);
        return session;
    }

    private static void UseSession(HttpClient client, string sessionToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

    [Fact]
    public async Task Issue_and_redeem_signs_the_user_in()
    {
        using var client = factory.CreateClient();
        var email = UniqueEmail();

        var token = await RequestTokenAsync(client, email);
        var session = await RedeemAsync(client, token);

        UseSession(client, session.Token);
        var me = await client.GetFromJsonAsync<MeResponse>("/auth/me");
        Assert.NotNull(me);
        Assert.Equal(email, me.Email);
    }

    [Fact]
    public async Task Second_link_for_the_same_address_signs_in_the_same_user()
    {
        using var client = factory.CreateClient();
        var email = UniqueEmail();

        var firstSession = await RedeemAsync(client, await RequestTokenAsync(client, email));
        UseSession(client, firstSession.Token);
        var firstMe = await client.GetFromJsonAsync<MeResponse>("/auth/me");

        var secondSession = await RedeemAsync(client, await RequestTokenAsync(client, email));
        UseSession(client, secondSession.Token);
        var secondMe = await client.GetFromJsonAsync<MeResponse>("/auth/me");

        Assert.NotNull(firstMe);
        Assert.NotNull(secondMe);
        Assert.Equal(firstMe.UserId, secondMe.UserId);
    }

    [Fact]
    public async Task A_magic_link_is_single_use()
    {
        using var client = factory.CreateClient();
        var token = await RequestTokenAsync(client, UniqueEmail());

        await RedeemAsync(client, token);
        var secondAttempt = await client.PostAsJsonAsync("/auth/sessions", new { token });

        Assert.Equal(HttpStatusCode.Unauthorized, secondAttempt.StatusCode);
    }

    [Fact]
    public async Task An_expired_magic_link_is_rejected()
    {
        // Same factory pipeline, but links are born expired.
        using var expiredFactory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Auth:MagicLinkLifetimeMinutes", "-1"));
        using var client = expiredFactory.CreateClient();
        var email = UniqueEmail();

        var request = await client.PostAsJsonAsync("/auth/magic-link", new { email });
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        var token = factory.Sender.TokenFor(email);

        var redeem = await client.PostAsJsonAsync("/auth/sessions", new { token });
        Assert.Equal(HttpStatusCode.Unauthorized, redeem.StatusCode);
    }

    [Fact]
    public async Task A_tampered_or_unknown_token_is_rejected()
    {
        using var client = factory.CreateClient();

        var redeem = await client.PostAsJsonAsync("/auth/sessions", new { token = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, redeem.StatusCode);
    }

    [Fact]
    public async Task An_unauthenticated_call_to_a_protected_endpoint_is_rejected()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_session_immediately()
    {
        using var client = factory.CreateClient();
        var session = await RedeemAsync(client, await RequestTokenAsync(client, UniqueEmail()));
        UseSession(client, session.Token);

        var logout = await client.DeleteAsync("/auth/sessions/current");
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var afterLogout = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task An_invalid_email_is_rejected_with_a_validation_problem()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/magic-link", new { email = "not-an-email" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
