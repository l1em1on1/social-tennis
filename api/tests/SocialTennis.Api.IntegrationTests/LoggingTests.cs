using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SocialTennis.Api.Features.Auth.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// Logs leave the app (Log Analytics keeps them for 30 days), so no sign-in
/// credential may appear in them (#115). The email sender is the capturing one
/// here; the Log email provider writes the link on purpose and is only allowed
/// where that is the point (#107).
/// </summary>
public class LoggingTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
{
    /// <summary>Every log event at every level, with its scopes, as text.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        public ConcurrentQueue<string> Lines { get; } = new();

        private IExternalScopeProvider scopes = new LoggerExternalScopeProvider();

        public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => scopes = scopeProvider;

        public void Dispose()
        {
        }

        private sealed class Logger(CapturingLoggerProvider provider, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => provider.scopes.Push(state);

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var scopeText = new List<string>();
                provider.scopes.ForEachScope((scope, list) => list.Add(scope?.ToString() ?? ""), scopeText);
                var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                    ? string.Join(" ", pairs.Select(p => $"{p.Key}={p.Value}"))
                    : "";
                provider.Lines.Enqueue(
                    $"{category} {formatter(state, exception)} {values} {string.Join(" ", scopeText)} {exception}");
            }
        }
    }

    [Fact]
    public async Task No_sign_in_link_or_session_token_appears_in_any_log_line()
    {
        var capture = new CapturingLoggerProvider();
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            // The factory's capturing sender replaces the Log provider; this
            // only lets the host start in Production (#107).
            builder.UseSetting("Email:AllowLogProvider", "true");
            builder.ConfigureLogging(logging =>
            {
                logging.AddProvider(capture);
                logging.SetMinimumLevel(LogLevel.Trace);
                logging.AddFilter(_ => true);
            });
        });
        using var client = host.CreateClient();
        var email = $"logging-{Guid.NewGuid():N}@example.test";

        // A whole sign-in: request a link, redeem it, use the session, sign out.
        (await client.PostAsJsonAsync("/auth/magic-link", new { email })).EnsureSuccessStatusCode();
        var link = factory.Sender.TokenFor(email);
        var redeemed = await client.PostAsJsonAsync("/auth/sessions", new { token = link });
        var session = (await redeemed.Content.ReadFromJsonAsync<RedeemMagicLinkResponse>())!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session);
        (await client.GetAsync("/auth/me")).EnsureSuccessStatusCode();
        (await client.DeleteAsync("/auth/sessions/current")).EnsureSuccessStatusCode();

        // The capture sees request-level logging, so an empty result means something.
        Assert.Contains(capture.Lines, line => line.Contains("/auth/sessions", StringComparison.Ordinal));
        Assert.DoesNotContain(capture.Lines, line => line.Contains(link, StringComparison.Ordinal));
        Assert.DoesNotContain(capture.Lines, line => line.Contains(session, StringComparison.Ordinal));
    }
}
