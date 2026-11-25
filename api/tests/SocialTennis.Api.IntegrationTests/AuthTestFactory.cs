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
        // Test clients send no client address, so every test is one client to
        // the per-client limit (#108). And every test in a class shares one app,
        // so one ceiling across all clients. Tests of either limit set it
        // themselves.
        builder.UseSetting("Auth:MagicLinkRequestsPerClientPerMinute", "100000");
        builder.UseSetting("Auth:MagicLinkRequestsPerMinute", "100000");
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

    /// <summary>
    /// Every factory signs in the same Admin address against the one database,
    /// and redeeming a link uses up that address's other links (#109). So the
    /// Admin's request-and-redeem never overlaps across test classes either.
    /// </summary>
    private static readonly SemaphoreSlim AdminSignInGate = new(1, 1);

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
            if (adminClient is null)
            {
                await AdminSignInGate.WaitAsync();
                try
                {
                    adminClient = await CreateSignedInClientAsync(AdminEmail);
                }
                finally
                {
                    AdminSignInGate.Release();
                }
            }

            return adminClient;
        }
        finally
        {
            adminGate.Release();
        }
    }

    /// <summary>
    /// A client signed in as a fresh User whom the Admin has made a manager,
    /// through the API as it happens for real: of these Clubs, or of the
    /// factory's one (<see cref="AClubIdAsync"/>).
    /// </summary>
    public async Task<HttpClient> CreateManagerClientAsync(params Guid[] clubs)
    {
        var client = await CreateSignedInClientAsync($"manager-{Guid.NewGuid():N}@example.test");
        foreach (var club in clubs.Length > 0 ? clubs : [await AClubIdAsync()])
        {
            await MakeManagerAsync(client, club);
        }

        return client;
    }

    /// <summary>The Admin makes this client's User a manager of the Club.</summary>
    public async Task MakeManagerAsync(HttpClient client, Guid club)
    {
        var me = await client.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me");
        var admin = await AdminClientAsync();
        (await admin.PutAsync($"/clubs/{club}/managers/{me!.UserId}", null)).EnsureSuccessStatusCode();
    }

    /// <summary>
    /// The Club's join link code (#222), turned on by the Admin if it's off.
    /// Turning on keeps a link that's on, so parallel asks share one code, and
    /// a test that resets a link gets the new one.
    /// </summary>
    public async Task<string> JoinCodeAsync(Guid clubId)
    {
        var admin = await AdminClientAsync();
        var turnedOn = await admin.PutAsync($"/clubs/{clubId}/join-link", null);
        turnedOn.EnsureSuccessStatusCode();
        return (await turnedOn.Content.ReadFromJsonAsync<ClubJoinLink>())!.Code!;
    }

    /// <summary>The <paramref name="client"/>'s Player asks to join the Club through its join link (#222).</summary>
    public async Task<HttpResponseMessage> AskToJoinAsync(HttpClient client, Guid clubId, Guid playerId) =>
        await client.PutAsync($"/join-links/{await JoinCodeAsync(clubId)}/members/{playerId}", null);

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
