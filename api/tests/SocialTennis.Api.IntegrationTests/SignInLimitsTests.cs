using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Auth.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// Requesting a sign-in link is limited per client, and asking for a link
/// creates no User (#108). The client is what the BFF reports in
/// X-Client-Address.
/// </summary>
public class SignInLimitsTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
{
    private static string UniqueEmail() => $"limits-{Guid.NewGuid():N}@example.test";

    /// <summary>A client address of its own, so tests and clients don't share buckets.</summary>
    private static string UniqueClient()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        return $"10.{bytes[0]}.{bytes[1]}.{bytes[2]}";
    }

    private static Task<HttpResponseMessage> RequestLinkAsync(
        HttpClient client, string email, string? clientAddress, string? forwardedFor = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/auth/magic-link")
        {
            Content = JsonContent.Create(new { email }),
        };
        if (clientAddress is not null)
        {
            message.Headers.Add("X-Client-Address", clientAddress);
        }

        if (forwardedFor is not null)
        {
            message.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return client.SendAsync(message);
    }

    private WebApplicationFactory<Program> With(params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

    [Fact]
    public async Task A_client_asking_a_sixth_time_in_a_minute_is_refused_while_another_client_is_not()
    {
        using var limited = With(("Auth:MagicLinkRequestsPerClientPerMinute", "5"));
        using var client = limited.CreateClient();
        var busy = UniqueClient();

        for (var i = 0; i < 5; i++)
        {
            var allowed = await RequestLinkAsync(client, UniqueEmail(), busy);
            Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
        }

        var sixth = await RequestLinkAsync(client, UniqueEmail(), busy);
        var other = await RequestLinkAsync(client, UniqueEmail(), UniqueClient());

        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        Assert.Equal("application/problem+json", sixth.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Accepted, other.StatusCode);
    }

    [Fact]
    public async Task A_forwarding_header_does_not_change_the_bucket()
    {
        // The API reads only the address the BFF reports, never X-Forwarded-For.
        using var limited = With(("Auth:MagicLinkRequestsPerClientPerMinute", "2"));
        using var client = limited.CreateClient();
        var address = UniqueClient();

        var first = await RequestLinkAsync(client, UniqueEmail(), address, forwardedFor: "198.51.100.1");
        var second = await RequestLinkAsync(client, UniqueEmail(), address, forwardedFor: "198.51.100.2");
        var third = await RequestLinkAsync(client, UniqueEmail(), address, forwardedFor: "198.51.100.3");

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    [Fact]
    public async Task Requests_with_no_client_address_share_one_bucket()
    {
        using var limited = With(("Auth:MagicLinkRequestsPerClientPerMinute", "1"));
        using var client = limited.CreateClient();

        var first = await RequestLinkAsync(client, UniqueEmail(), clientAddress: null);
        var second = await RequestLinkAsync(client, UniqueEmail(), clientAddress: "not an address");

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task A_client_refused_by_its_own_limit_does_not_use_up_the_ceiling()
    {
        using var limited = With(
            ("Auth:MagicLinkRequestsPerClientPerMinute", "1"),
            ("Auth:MagicLinkRequestsPerMinute", "3"));
        using var client = limited.CreateClient();
        var busy = UniqueClient();

        for (var i = 0; i < 6; i++)
        {
            await RequestLinkAsync(client, UniqueEmail(), busy);
        }

        var second = await RequestLinkAsync(client, UniqueEmail(), UniqueClient());
        var third = await RequestLinkAsync(client, UniqueEmail(), UniqueClient());
        var fourth = await RequestLinkAsync(client, UniqueEmail(), UniqueClient());

        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, third.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
    }

    [Fact]
    public async Task One_client_filling_an_address_quota_does_not_stop_another_client_getting_a_link()
    {
        using var limited = With(
            ("Auth:MaxLiveMagicLinksPerAddressAndClient", "2"),
            ("Auth:MaxLiveMagicLinksPerAddress", "10"));
        using var client = limited.CreateClient();
        var email = UniqueEmail();
        var filler = UniqueClient();

        for (var i = 0; i < 4; i++)
        {
            var response = await RequestLinkAsync(client, email, filler);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }

        Assert.Equal(2, factory.Sender.SentTo(email));

        var owner = await RequestLinkAsync(client, email, UniqueClient());

        Assert.Equal(HttpStatusCode.Accepted, owner.StatusCode);
        Assert.Equal(3, factory.Sender.SentTo(email));
    }

    [Fact]
    public async Task Every_client_together_is_held_to_the_address_total()
    {
        using var limited = With(
            ("Auth:MaxLiveMagicLinksPerAddressAndClient", "2"),
            ("Auth:MaxLiveMagicLinksPerAddress", "3"));
        using var client = limited.CreateClient();
        var email = UniqueEmail();

        for (var i = 0; i < 4; i++)
        {
            var response = await RequestLinkAsync(client, email, UniqueClient());
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }

        Assert.Equal(3, factory.Sender.SentTo(email));
    }

    [Fact]
    public async Task Asking_for_a_link_creates_no_User_and_redeeming_it_does()
    {
        using var client = factory.CreateClient();
        var email = UniqueEmail();

        var request = await RequestLinkAsync(client, email, UniqueClient());
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        Assert.False(await UserExistsAsync(email));

        var redeem = await client.PostAsJsonAsync("/auth/sessions", new { token = factory.Sender.TokenFor(email) });
        Assert.Equal(HttpStatusCode.OK, redeem.StatusCode);
        Assert.NotNull(await redeem.Content.ReadFromJsonAsync<RedeemMagicLinkResponse>());
        Assert.True(await UserExistsAsync(email));
    }

    [Fact]
    public async Task Two_links_for_a_new_address_redeemed_at_once_make_one_User()
    {
        using var client = factory.CreateClient();
        var email = UniqueEmail();
        await RequestLinkAsync(client, email, UniqueClient());
        var first = factory.Sender.TokenFor(email);
        await RequestLinkAsync(client, email, UniqueClient());
        var second = factory.Sender.TokenFor(email);

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/auth/sessions", new { token = first }),
            client.PostAsJsonAsync("/auth/sessions", new { token = second }));

        // Redeeming one link uses up the other (#109), so one of them may be a
        // 401; neither is a 500, and there is one User.
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(responses, r => Assert.True(
            r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized, $"got {r.StatusCode}"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == email));
    }

    private async Task<bool> UserExistsAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
        return await db.Users.AnyAsync(u => u.Email == email);
    }
}
