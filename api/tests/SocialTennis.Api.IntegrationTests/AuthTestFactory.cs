using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SocialTennis.Api.Email;
using SocialTennis.Api.Features.Auth.Contracts;
using SocialTennis.Api.Features.Clubs.Contracts;

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

    private readonly SemaphoreSlim adminGate = new(1, 1);
    private HttpClient? adminClient;
    private Guid? clubId;

    /// <summary>
    /// The Admin named in Auth:AdminEmails, signed in once per factory and
    /// shared: don't dispose it. One sign-in, because <see cref="CapturingEmailSender.TokenFor"/>
    /// reads the newest email to an address, which two sign-ins at once would
    /// both take.
    /// </summary>
    public async Task<HttpClient> AdminClientAsync()
    {
        await adminGate.WaitAsync();
        try
        {
            return adminClient ??= await CreateSignedInClientAsync(AdminEmail);
        }
        finally
        {
            adminGate.Release();
        }
    }

    /// <summary>
    /// A client signed in as a fresh User whom the Admin has made a Manager,
    /// through the API as it happens for real.
    /// </summary>
    public async Task<HttpClient> CreateManagerClientAsync()
    {
        var client = await CreateSignedInClientAsync($"manager-{Guid.NewGuid():N}@example.test");
        var me = await client.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me");

        var admin = await AdminClientAsync();
        (await admin.PutAsync($"/users/{me!.UserId}/roles/Manager", null)).EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>
    /// A Club for tests that need one and don't care which. There is no seeded
    /// Club; the Admin creates this one, once per factory.
    /// </summary>
    public async Task<Guid> AClubIdAsync()
    {
        var admin = await AdminClientAsync();
        await adminGate.WaitAsync();
        try
        {
            if (clubId is null)
            {
                var created = await admin.PostAsJsonAsync("/clubs", new { name = $"Test Club {Guid.NewGuid():N}" });
                created.EnsureSuccessStatusCode();
                clubId = (await created.Content.ReadFromJsonAsync<CreateClubResponse>())!.Id;
            }

            return clubId.Value;
        }
        finally
        {
            adminGate.Release();
        }
    }
}
