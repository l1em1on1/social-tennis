using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Email;
using SocialTennis.Api.Features.Auth.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// Captures emails instead of sending them — the only service the tests
/// replace. Everything else (HTTP boundary, real Postgres, the code that
/// writes the sign-in email) stays real: anything touching the database is
/// tested here, over HTTP, never against a fake or in-memory provider. Pure
/// logic with no DbContext may instead be unit-tested in
/// SocialTennis.Api.UnitTests (ADR-0010).
/// </summary>
public partial class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentDictionary<string, OutgoingEmail> _lastByRecipient = new();
    private readonly ConcurrentDictionary<string, int> _sentByRecipient = new();

    public Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        _lastByRecipient[email.To] = email;
        _sentByRecipient.AddOrUpdate(email.To, 1, (_, sent) => sent + 1);
        return Task.CompletedTask;
    }

    /// <summary>How many emails were actually sent to this address.</summary>
    public int SentTo(string email) =>
        _sentByRecipient.GetValueOrDefault(email.Trim().ToLowerInvariant());

    /// <summary>The last email sent to this address.</summary>
    public OutgoingEmail LastTo(string email) => _lastByRecipient[email.Trim().ToLowerInvariant()];

    /// <summary>The sign-in link in the last email to this address, as its recipient would find it.</summary>
    public Uri LinkFor(string email) => new(LinkPattern().Match(LastTo(email).PlainText).Value);

    public string TokenFor(string email)
    {
        var query = LinkFor(email).Query.TrimStart('?');
        var token = query.Split('&')
            .Select(pair => pair.Split('=', 2))
            .First(kv => kv[0] == "token")[1];
        return Uri.UnescapeDataString(token);
    }

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex LinkPattern();
}

public class AuthTestFactory : WebApplicationFactory<Program>
{
    /// <summary>Listed in Auth:AdminEmails, so signing in with it is an Admin.</summary>
    public const string AdminEmail = "admin@example.test";

    public CapturingEmailSender Sender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Sender);
        });
        builder.UseSetting("Auth:AdminEmails", $"someone@example.test, {AdminEmail}");
    }

    /// <summary>
    /// A client signed in as a fresh User, through the real magic-link flow.
    /// The API denies by default (ADR-0017), so every test that isn't about
    /// the anonymous routes starts here.
    /// </summary>
    public Task<HttpClient> CreateSignedInClientAsync() =>
        CreateSignedInClientAsync($"player-{Guid.NewGuid():N}@example.test");

    /// <summary>A client signed in as the User with this email, creating them if need be.</summary>
    public async Task<HttpClient> CreateSignedInClientAsync(string email)
    {
        var client = CreateClient();

        var request = await client.PostAsJsonAsync("/auth/magic-link", new { email });
        request.EnsureSuccessStatusCode();
        var redeem = await client.PostAsJsonAsync("/auth/sessions", new { token = Sender.TokenFor(email) });
        redeem.EnsureSuccessStatusCode();
        var session = await redeem.Content.ReadFromJsonAsync<RedeemMagicLinkResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.Token);
        return client;
    }

    /// <summary>
    /// A client signed in as a fresh User holding the Manager role. The role is
    /// written to the database: granting it through the API is issue #18.
    /// </summary>
    public async Task<HttpClient> CreateManagerClientAsync()
    {
        var email = $"manager-{Guid.NewGuid():N}@example.test";
        var client = await CreateSignedInClientAsync(email);

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
        await db.Users.Where(u => u.Email == email)
            .ExecuteUpdateAsync(u => u.SetProperty(user => user.Roles, UserRoles.Manager));
        return client;
    }
}
