using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SocialTennis.Api.Authentication;

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

    public Task SendAsync(string email, string link, CancellationToken cancellationToken)
    {
        _lastLinkByEmail[email] = link;
        return Task.CompletedTask;
    }

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
}
