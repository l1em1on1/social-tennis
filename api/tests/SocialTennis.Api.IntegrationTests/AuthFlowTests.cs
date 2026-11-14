using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Email;
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

    private static async Task<RedeemMagicLinkResponse> RedeemAsync(HttpClient client, string token)
    {
        var response = await client.PostAsJsonAsync("/auth/sessions", new { token });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<RedeemMagicLinkResponse>();
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
        var me = await client.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me");
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
        var firstMe = await client.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me");

        var secondSession = await RedeemAsync(client, await RequestTokenAsync(client, email));
        UseSession(client, secondSession.Token);
        var secondMe = await client.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me");

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
    public async Task Concurrent_first_requests_for_one_address_all_succeed()
    {
        // Both see no User and both insert one; the unique index lets one win.
        using var client = factory.CreateClient();
        var email = UniqueEmail();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => client.PostAsJsonAsync("/auth/magic-link", new { email })));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));
        await RedeemAsync(client, factory.Sender.TokenFor(email));
    }

    [Fact]
    public async Task An_address_holding_its_quota_of_live_links_gets_202_but_no_more_email()
    {
        using var limitedFactory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Auth:MaxLiveMagicLinksPerAddress", "2"));
        using var client = limitedFactory.CreateClient();
        var email = UniqueEmail();

        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync("/auth/magic-link", new { email });
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }

        Assert.Equal(2, factory.Sender.SentTo(email));
    }

    [Fact]
    public async Task Concurrent_requests_at_the_quota_boundary_do_not_exceed_it()
    {
        using var limitedFactory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Auth:MaxLiveMagicLinksPerAddress", "2"));
        using var client = limitedFactory.CreateClient();
        var email = UniqueEmail();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync("/auth/magic-link", new { email })));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));
        Assert.Equal(2, factory.Sender.SentTo(email));
    }

    [Fact]
    public async Task Magic_link_requests_beyond_the_ceiling_are_rate_limited()
    {
        using var limitedFactory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Auth:MagicLinkRequestsPerMinute", "2"));
        using var client = limitedFactory.CreateClient();

        await client.PostAsJsonAsync("/auth/magic-link", new { email = UniqueEmail() });
        await client.PostAsJsonAsync("/auth/magic-link", new { email = UniqueEmail() });
        var third = await client.PostAsJsonAsync("/auth/magic-link", new { email = UniqueEmail() });

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal("application/problem+json", third.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task An_unhandled_error_is_a_problem_details_500()
    {
        // Production, where the developer exception page is off.
        using var failingFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMagicLinkSender>();
                services.AddSingleton<IMagicLinkSender, ThrowingMagicLinkSender>();
            });
        });
        using var client = failingFactory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/magic-link", new { email = UniqueEmail() });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task The_email_carries_the_link_to_the_web_verify_route()
    {
        using var client = factory.CreateClient();
        var email = UniqueEmail();

        await RequestTokenAsync(client, email);

        var sent = factory.Sender.LastTo(email);
        var link = factory.Sender.LinkFor(email);
        Assert.Equal("http://localhost:3000/auth/verify", link.GetLeftPart(UriPartial.Path));
        Assert.Contains(System.Net.WebUtility.HtmlEncode(link.OriginalString), sent.Html, StringComparison.Ordinal);
        Assert.Contains("expires in 15 minutes", sent.PlainText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_delivery_is_a_503_and_retrying_works()
    {
        // The address may hold one live link. If the failed attempt kept its
        // token, the retry would be answered 202 and send nothing.
        var mail = new FailingOnceEmailSender(factory.Sender);
        using var flakyFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:MaxLiveMagicLinksPerAddress", "1");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(mail);
            });
        });
        using var client = flakyFactory.CreateClient();
        var email = UniqueEmail();

        var failed = await client.PostAsJsonAsync("/auth/magic-link", new { email });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Equal("application/problem+json", failed.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, factory.Sender.SentTo(email));

        var retried = await client.PostAsJsonAsync("/auth/magic-link", new { email });

        Assert.Equal(HttpStatusCode.Accepted, retried.StatusCode);
        Assert.Equal(1, factory.Sender.SentTo(email));
        await RedeemAsync(client, factory.Sender.TokenFor(email));
    }

    /// <summary>The mail provider is down for the first email, then recovers.</summary>
    private sealed class FailingOnceEmailSender(IEmailSender inner) : IEmailSender
    {
        private int _attempts;

        public Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken) =>
            Interlocked.Increment(ref _attempts) == 1
                ? throw new EmailDeliveryException("The mail provider is down.", new HttpRequestException())
                : inner.SendAsync(email, cancellationToken);
    }

    [Fact]
    public async Task Health_reports_healthy_without_a_session()
    {
        using var client = factory.CreateClient();

        var live = await client.GetAsync("/health");
        var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public async Task Liveness_ignores_a_failing_readiness_check()
    {
        // Stands in for an unreachable database: /health must not run it.
        using var unreadyFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddHealthChecks().AddCheck(
                    "always-down",
                    () => HealthCheckResult.Unhealthy(),
                    tags: [Program.ReadyTag])));
        using var client = unreadyFactory.CreateClient();

        var live = await client.GetAsync("/health");
        var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
    }

    private sealed class ThrowingMagicLinkSender : IMagicLinkSender
    {
        public Task SendAsync(string email, string link, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The mail provider is down.");
    }

    [Fact]
    public async Task An_invalid_email_is_rejected_with_a_validation_problem()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/magic-link", new { email = "not-an-email" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
