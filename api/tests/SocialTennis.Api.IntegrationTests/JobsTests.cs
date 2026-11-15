using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Auth.Contracts;
using SocialTennis.Api.Jobs;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// Scheduled jobs (#112), run the way the Container Apps Job runs them: through
/// <see cref="JobRunner"/>, against the real database.
/// </summary>
/// <remarks>
/// The database is shared with every other test class, and a purge is
/// database-wide. So the clock is only ever moved back, to create a link that
/// is already expired, never forward past other tests' live links.
/// </remarks>
public class JobsTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
{
    private sealed class MovableClock : TimeProvider
    {
        public DateTimeOffset? Fixed { get; set; }

        public override DateTimeOffset GetUtcNow() => Fixed ?? System.GetUtcNow();
    }

    private sealed record TokenState(int UsedLinks, int LiveLinks, int ExpiredLinks, int LiveSessions, int RevokedSessions);

    private static async Task<TokenState> StateAsync(IServiceProvider services, string email)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
        var now = DateTimeOffset.UtcNow;
        var links = await db.MagicLinkTokens.Where(t => t.User.Email == email).ToListAsync();
        var sessions = await db.SessionTokens.Where(s => s.User.Email == email).ToListAsync();
        return new TokenState(
            links.Count(t => t.UsedAt != null),
            links.Count(t => t.UsedAt == null && t.ExpiresAt > now),
            links.Count(t => t.UsedAt == null && t.ExpiresAt <= now),
            sessions.Count(s => s.RevokedAt == null && s.ExpiresAt > now),
            sessions.Count(s => s.RevokedAt != null));
    }

    [Fact]
    public async Task Purging_tokens_deletes_spent_links_and_ended_sessions_and_nothing_live()
    {
        var clock = new MovableClock();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using var client = host.CreateClient();
        var email = $"jobs-{Guid.NewGuid():N}@example.test";

        async Task<string> SignInAsync()
        {
            (await client.PostAsJsonAsync("/auth/magic-link", new { email })).EnsureSuccessStatusCode();
            var redeemed = await client.PostAsJsonAsync("/auth/sessions", new { token = factory.Sender.TokenFor(email) });
            return (await redeemed.Content.ReadFromJsonAsync<RedeemMagicLinkResponse>())!.Token;
        }

        // A session that was signed out (revoked), then one still live.
        var signedOut = await SignInAsync();
        using (var request = new HttpRequestMessage(HttpMethod.Delete, "/auth/sessions/current"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", signedOut);
            (await client.SendAsync(request)).EnsureSuccessStatusCode();
        }
        var live = await SignInAsync();

        // A link requested an hour ago, so expired now; and one requested now.
        clock.Fixed = DateTimeOffset.UtcNow.AddHours(-1);
        (await client.PostAsJsonAsync("/auth/magic-link", new { email })).EnsureSuccessStatusCode();
        clock.Fixed = null;
        (await client.PostAsJsonAsync("/auth/magic-link", new { email })).EnsureSuccessStatusCode();
        var unusedToken = factory.Sender.TokenFor(email);

        Assert.Equal(new TokenState(UsedLinks: 2, LiveLinks: 1, ExpiredLinks: 1, LiveSessions: 1, RevokedSessions: 1),
            await StateAsync(host.Services, email));

        Assert.Equal(JobRunner.Succeeded, await JobRunner.RunAsync(host.Services, "purge-tokens", CancellationToken.None));

        Assert.Equal(new TokenState(UsedLinks: 0, LiveLinks: 1, ExpiredLinks: 0, LiveSessions: 1, RevokedSessions: 0),
            await StateAsync(host.Services, email));

        // What is left still works.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", live);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/auth/sessions", new { token = unusedToken })).StatusCode);
    }

    [Fact]
    public async Task Purging_twice_gives_the_same_result()
    {
        using var client = await factory.CreateSignedInClientAsync();
        var me = await client.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me");

        Assert.Equal(JobRunner.Succeeded, await JobRunner.RunAsync(factory.Services, "purge-tokens", CancellationToken.None));
        var once = await StateAsync(factory.Services, me!.Email);
        Assert.Equal(JobRunner.Succeeded, await JobRunner.RunAsync(factory.Services, "purge-tokens", CancellationToken.None));

        Assert.Equal(once, await StateAsync(factory.Services, me.Email));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/auth/me")).StatusCode);
    }

    [Fact]
    public async Task An_unknown_job_name_exits_with_its_own_code()
    {
        Assert.Equal(JobRunner.UnknownJob, await JobRunner.RunAsync(factory.Services, "no-such-job", CancellationToken.None));
    }
}
