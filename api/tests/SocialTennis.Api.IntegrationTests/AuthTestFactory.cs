using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Features.Auth.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// Captures magic links instead of sending them — the only service the tests
/// replace. Everything else (HTTP boundary, real Postgres) stays real: anything
/// touching the database is tested here, over HTTP, never against a fake or
/// in-memory provider. Pure logic with no DbContext may instead be unit-tested
/// in SocialTennis.Api.UnitTests (ADR-0010).
/// </summary>
public class CapturingMagicLinkSender : IMagicLinkSender
{
    private readonly ConcurrentDictionary<string, string> _lastLinkByEmail = new();
    private readonly ConcurrentDictionary<string, int> _sentByEmail = new();

    public Task SendAsync(string email, string link, CancellationToken cancellationToken)
    {
        _lastLinkByEmail[email] = link;
        _sentByEmail.AddOrUpdate(email, 1, (_, sent) => sent + 1);
        return Task.CompletedTask;
    }

    /// <summary>How many links were actually sent to this address.</summary>
    public int SentTo(string email) =>
        _sentByEmail.GetValueOrDefault(email.Trim().ToLowerInvariant());

    public string TokenFor(string email)
    {
        var link = _lastLinkByEmail[email.Trim().ToLowerInvariant()];
        var query = new Uri(link).Query.TrimStart('?');
        var token = query.Split('&')
            .Select(pair => pair.Split('=', 2))
            .First(kv => kv[0] == "token")[1];
        return Uri.UnescapeDataString(token);
    }
}

public class AuthTestFactory : WebApplicationFactory<Program>
{
    public CapturingMagicLinkSender Sender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMagicLinkSender>();
            services.AddSingleton<IMagicLinkSender>(Sender);
        });
    }

    /// <summary>
    /// A client signed in as a fresh User, through the real magic-link flow.
    /// The API denies by default (ADR-0017), so every test that isn't about
    /// the anonymous routes starts here.
    /// </summary>
    public async Task<HttpClient> CreateSignedInClientAsync()
    {
        var client = CreateClient();
        var email = $"player-{Guid.NewGuid():N}@example.test";

        var request = await client.PostAsJsonAsync("/auth/magic-link", new { email });
        request.EnsureSuccessStatusCode();
        var redeem = await client.PostAsJsonAsync("/auth/sessions", new { token = Sender.TokenFor(email) });
        redeem.EnsureSuccessStatusCode();
        var session = await redeem.Content.ReadFromJsonAsync<RedeemMagicLinkResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.Token);
        return client;
    }
}
