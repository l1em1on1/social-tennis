using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SocialTennis.Api.Access;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Features.Auth.Contracts;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Features.Leagues.Contracts;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// Who may call what (#110, ADR-0017): every operation the API has, and what
/// each kind of caller gets from it. A new endpoint fails the first test until
/// it is listed here, so leaving a route open to any signed-in User is a
/// reviewed line in this file.
/// </summary>
public partial class ManagerAccessTests(ManagerAccessTests.Factory factory) : IClassFixture<ManagerAccessTests.Factory>
{
    private const string Anonymous = "anonymous";
    private const string SignedIn = "signed in";

    private static readonly Dictionary<string, string> Operations = new()
    {
        ["GET /health"] = Anonymous,
        ["GET /health/ready"] = Anonymous,
        ["GET /openapi/{documentName}.json"] = Anonymous,
        ["POST /auth/magic-link"] = Anonymous,
        ["POST /auth/sessions"] = Anonymous,
        ["GET /auth/me"] = SignedIn,
        ["DELETE /auth/sessions/current"] = SignedIn,

        ["GET /clubs"] = Policies.AnyManager,
        ["POST /clubs"] = Policies.Admin,
        ["PUT /clubs/{clubId}"] = Policies.Admin,
        ["PUT /clubs/{clubId}/managers/{userId}"] = Policies.Admin,
        ["DELETE /clubs/{clubId}/managers/{userId}"] = Policies.Admin,
        ["PUT /clubs/{clubId}/league-managers/{userId}"] = Policies.ClubManager,
        ["DELETE /clubs/{clubId}/league-managers/{userId}"] = Policies.ClubManager,
        ["GET /clubs/{clubId}/members"] = Policies.ClubManager,
        ["PUT /clubs/{clubId}/members/{playerId}/approval"] = Policies.ClubManager,
        ["DELETE /clubs/{clubId}/members/{playerId}"] = Policies.ClubManager,
        ["POST /clubs/{clubId}/players"] = Policies.ClubManager,
        ["POST /clubs/{clubId}/invites"] = Policies.ClubManager,
        ["DELETE /clubs/{clubId}/invites/{inviteId}"] = Policies.ClubManager,
        ["GET /clubs/{clubId}/join-link"] = Policies.ClubManager,
        ["PUT /clubs/{clubId}/join-link"] = Policies.ClubManager,
        ["POST /clubs/{clubId}/join-link/reset"] = Policies.ClubManager,
        ["DELETE /clubs/{clubId}/join-link"] = Policies.ClubManager,
        ["GET /join-links/{code}"] = SignedIn,
        ["PUT /join-links/{code}/members/{playerId}"] = SignedIn,

        ["GET /players"] = SignedIn,
        ["POST /players"] = SignedIn,
        ["PUT /players/{playerId}"] = SignedIn,
        ["PUT /players/{playerId}/rating"] = Policies.PlayerManager,
        ["DELETE /players/{playerId}/clubs/{clubId}"] = SignedIn,
        ["GET /players/{playerId}/leagues"] = SignedIn,
        ["GET /players/{playerId}/games"] = SignedIn,

        ["GET /users"] = Policies.Admin,
        ["PUT /users/{userId}/roles/{role}"] = Policies.Admin,
        ["DELETE /users/{userId}/roles/{role}"] = Policies.Admin,
        ["DELETE /users/{userId}"] = Policies.Admin,

        ["GET /clubs/{clubId}/leagues"] = Policies.ClubStaff,
        ["POST /clubs/{clubId}/leagues"] = Policies.LeagueCreator,
        ["GET /leagues/{leagueId}"] = Policies.LeagueManager,
        ["PUT /leagues/{leagueId}"] = Policies.LeagueManager,
        ["PUT /leagues/{leagueId}/managers/{userId}"] = Policies.LeagueClubManager,
        ["DELETE /leagues/{leagueId}/managers/{userId}"] = Policies.LeagueClubManager,
        ["GET /leagues/{leagueId}/addable-players"] = Policies.LeagueManager,
        ["PUT /leagues/{leagueId}/participants/{playerId}"] = Policies.LeagueManager,
        ["DELETE /leagues/{leagueId}/participants/{playerId}"] = Policies.LeagueManager,
        ["POST /leagues/{leagueId}/teams"] = Policies.LeagueManager,
        ["DELETE /leagues/{leagueId}/teams/{teamId}"] = Policies.LeagueManager,
        ["GET /leagues/{leagueId}/start"] = Policies.LeagueManager,
        ["POST /leagues/{leagueId}/start"] = Policies.LeagueManager,
        ["GET /leagues/{leagueId}/close"] = Policies.LeagueManager,
        ["POST /leagues/{leagueId}/close"] = Policies.LeagueManager,
        ["POST /leagues/{leagueId}/moves"] = Policies.LeagueManager,
        ["PUT /leagues/{leagueId}/join-requests/{playerId}/approval"] = Policies.LeagueManager,
        ["PUT /leagues/{leagueId}/join-requests/{playerId}/rejection"] = Policies.LeagueManager,
        // The League's Players and its managers: the handler asks the same rule, and answers 404 to anyone else.
        ["GET /leagues/{leagueId}/ladder"] = SignedIn,
        ["PUT /leagues/{leagueId}/tie-marks"] = SignedIn,
        ["GET /leagues/{leagueId}/players/{playerId}/availability"] = SignedIn,
        ["PUT /leagues/{leagueId}/players/{playerId}/availability"] = SignedIn,
        ["POST /leagues/{leagueId}/join-requests"] = SignedIn,
        ["DELETE /leagues/{leagueId}/join-requests/{playerId}"] = SignedIn,
        ["GET /leagues/{leagueId}/partners"] = SignedIn,

        // The same three: the Game's League's Players and managers.
        ["GET /games/{gameId}"] = SignedIn,
        ["PUT /games/{gameId}/date"] = SignedIn,
        ["POST /games/{gameId}/score"] = SignedIn,
        ["PUT /games/{gameId}/score"] = SignedIn,
        ["PUT /games/{gameId}/score/override"] = Policies.LeagueManager,
        ["POST /games/{gameId}/score/record"] = Policies.LeagueManager,
        ["PUT /games/{gameId}/not-played"] = Policies.LeagueManager,
        ["DELETE /games/{gameId}/not-played"] = Policies.LeagueManager,
    };

    [Fact]
    public void Every_operation_says_who_may_call_it()
    {
        var actual = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                .Select(method => ($"{method} {Path(endpoint)}", WhoMayCall(endpoint))))
            .ToDictionary(operation => operation.Item1, operation => operation.Item2);

        Assert.Equal(
            Operations.OrderBy(o => o.Key, StringComparer.Ordinal),
            actual.OrderBy(o => o.Key, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Each_kind_of_caller_gets_what_the_table_says()
    {
        var world = await WorldAsync();
        var wrong = new List<string>();
        foreach (var (operation, policy) in Operations.Where(o => o.Value is not (Anonymous or SignedIn)))
        {
            foreach (var (who, client) in world.Callers)
            {
                var expected = Expected(policy, who);
                var answer = await AskAsync(client, operation, world.Ids);
                if (answer != expected)
                {
                    wrong.Add($"{operation} ({policy}) for {who}: {answer}, not {expected}");
                }
            }
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
    }

    [Fact]
    public async Task Every_policys_refusal_is_in_the_contract()
    {
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");

        bool Declares(string operation, string status)
        {
            var parts = operation.Split(' ');
            return paths.GetProperty(parts[1]).GetProperty(parts[0].ToLowerInvariant())
                .GetProperty("responses").TryGetProperty(status, out _);
        }

        var guarded = Operations.Where(o => o.Value is not (Anonymous or SignedIn)).ToList();
        Assert.DoesNotContain(guarded, o => !Declares(o.Key, "403"));
        Assert.DoesNotContain(guarded, o => o.Value is not (Policies.Admin or Policies.AnyManager) && !Declares(o.Key, "404"));
    }

    private static HttpStatusCode Expected(string policy, string who) => (policy, who) switch
    {
        (_, "nobody") => HttpStatusCode.Unauthorized,
        (_, "an Admin") => Allowed,
        (Policies.Admin, _) => HttpStatusCode.Forbidden,
        (_, "the Club's manager") => Allowed,
        (Policies.AnyManager, "another Club's manager" or TheLeaguesManager or AnotherLeaguesManager or RoleHolder) => Allowed,
        // Nothing to hide: the route names nothing.
        (Policies.AnyManager, _) => HttpStatusCode.Forbidden,
        (Policies.ClubStaff, TheLeaguesManager or AnotherLeaguesManager or RoleHolder) => Allowed,
        (Policies.LeagueCreator, RoleHolder) => Allowed,
        (Policies.LeagueManager, TheLeaguesManager) => Allowed,
        // They can see what they asked about, or they can't (ADR-0017).
        (_, "a member" or TheLeaguesManager or AnotherLeaguesManager or RoleHolder) => HttpStatusCode.Forbidden,
        (Policies.LeagueManager or Policies.LeagueClubManager, "a participant who left the Club") => HttpStatusCode.Forbidden,
        _ => HttpStatusCode.NotFound,
    };

    // Each has left the Club since, so it's the assignment or the role alone that counts.
    private const string TheLeaguesManager = "the League's manager";
    private const string AnotherLeaguesManager = "another League's manager";
    private const string RoleHolder = "a League manager with no League";

    [Fact]
    public async Task What_isnt_there_answers_as_what_is_hidden_does()
    {
        var world = await WorldAsync();
        var gone = world.Ids.Keys.Where(k => k != "role").ToDictionary(k => k, _ => Guid.NewGuid().ToString());
        foreach (var (operation, policy) in Operations.Where(o => o.Value is not (Anonymous or SignedIn or Policies.Admin or Policies.AnyManager)))
        {
            foreach (var who in new[] { "the Club's manager", TheLeaguesManager, RoleHolder, "a member", "an outsider" })
            {
                Assert.Equal((operation, who, HttpStatusCode.NotFound), (operation, who, await AskAsync(world.Callers[who], operation, gone)));
            }
        }
    }

    private static readonly int[] OneDivisionOfTwo = [2];

    private sealed record World(Dictionary<string, HttpClient> Callers, Dictionary<string, string> Ids);

    /// <summary>A Club with a started League of two, and a caller of each kind.</summary>
    private async Task<World> WorldAsync()
    {
        var admin = await factory.AdminClientAsync();
        var clubId = await factory.AClubIdAsync();
        var manager = await factory.CreateManagerClientAsync(clubId);
        var elsewhere = await admin.PostAsJsonAsync("/clubs", new { name = $"Elsewhere {Guid.NewGuid():N}" });
        var elsewhereId = (await elsewhere.Content.ReadFromJsonAsync<CreateClubResponse>())!.Id;
        var (player, playerId) = await MemberAsync(manager, clubId, "Access One");
        var (gone, goneId) = await MemberAsync(manager, clubId, "Access Two");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = await manager.PostAsJsonAsync($"/clubs/{clubId}/leagues", new
        {
            name = "Access League",
            type = "SinglesMixed",
            structure = "Divisional",
            startDate = today,
            endDate = today.AddMonths(3),
            stageLength = new { count = 1, unit = "Months" },
            stageRoll = "Manual",
            scoreFormat = new { gamesPerSet = 6, setsPerGame = 1, matchTiebreakDecider = false },
        });
        created.EnsureSuccessStatusCode();
        var leagueId = (await created.Content.ReadFromJsonAsync<CreateLeagueResponse>())!.Id;
        foreach (var id in new[] { playerId, goneId })
        {
            (await manager.PutAsync($"/leagues/{leagueId}/participants/{id}", null)).EnsureSuccessStatusCode();
        }

        var league = (await manager.GetFromJsonAsync<GetLeagueResponse>($"/leagues/{leagueId}"))!;
        (await manager.PostAsJsonAsync($"/leagues/{leagueId}/start", new
        {
            ladder = league.Teams.Select(t => t.Id),
            divisions = OneDivisionOfTwo,
            leftOut = Array.Empty<Guid>(),
            removed = Array.Empty<Guid>(),
        })).EnsureSuccessStatusCode();
        var ladder = (await player.GetFromJsonAsync<GetLeagueLadderResponse>($"/leagues/{leagueId}/ladder"))!;
        (await gone.DeleteAsync($"/players/{goneId}/clubs/{clubId}")).EnsureSuccessStatusCode();

        var other = await manager.PostAsJsonAsync($"/clubs/{clubId}/leagues", new
        {
            name = "Another Access League",
            type = "SinglesMixed",
            structure = "Divisional",
            startDate = today,
            endDate = today.AddMonths(3),
            stageLength = new { count = 1, unit = "Months" },
            stageRoll = "Manual",
            scoreFormat = new { gamesPerSet = 6, setsPerGame = 1, matchTiebreakDecider = false },
        });
        other.EnsureSuccessStatusCode();
        var otherLeagueId = (await other.Content.ReadFromJsonAsync<CreateLeagueResponse>())!.Id;
        var leaguesManager = await LeftTheClubAsync(manager, clubId, "League Boss", id => manager.PutAsync($"/leagues/{leagueId}/managers/{id}", null));
        var othersManager = await LeftTheClubAsync(manager, clubId, "Other Boss", id => manager.PutAsync($"/leagues/{otherLeagueId}/managers/{id}", null));
        var roleHolder = await LeftTheClubAsync(manager, clubId, "Role Holder", id => manager.PutAsync($"/clubs/{clubId}/league-managers/{id}", null));

        return new World(
            new Dictionary<string, HttpClient>
            {
                ["nobody"] = factory.CreateClient(),
                ["an Admin"] = admin,
                ["the Club's manager"] = manager,
                ["another Club's manager"] = await factory.CreateManagerClientAsync(elsewhereId),
                ["a member"] = player,
                [TheLeaguesManager] = leaguesManager,
                [AnotherLeaguesManager] = othersManager,
                [RoleHolder] = roleHolder,
                ["a participant who left the Club"] = gone,
                ["an outsider"] = await factory.CreateSignedInClientAsync(),
            },
            new Dictionary<string, string>
            {
                ["clubId"] = clubId.ToString(),
                ["leagueId"] = leagueId.ToString(),
                ["gameId"] = ladder.Divisions.Single().Games.Single().Id.ToString(),
                ["playerId"] = playerId.ToString(),
                ["role"] = "Admin",
            });
    }

    /// <summary>A member who is given something to run, and then leaves the Club: it stays theirs until it's taken (ADR-0034).</summary>
    private async Task<HttpClient> LeftTheClubAsync(HttpClient manager, Guid clubId, string name, Func<Guid, Task<HttpResponseMessage>> give)
    {
        var (client, playerId) = await MemberAsync(manager, clubId, name);
        var me = (await client.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!;
        (await give(me.UserId)).EnsureSuccessStatusCode();
        (await client.DeleteAsync($"/players/{playerId}/clubs/{clubId}")).EnsureSuccessStatusCode();
        return client;
    }

    private async Task<(HttpClient Client, Guid PlayerId)> MemberAsync(HttpClient manager, Guid clubId, string name)
    {
        var client = await factory.CreateSignedInClientAsync();
        var created = await client.PostAsJsonAsync("/players", new { name });
        var playerId = (await created.Content.ReadFromJsonAsync<CreatePlayerResponse>())!.Id;
        (await factory.AskToJoinAsync(client, clubId, playerId)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/clubs/{clubId}/members/{playerId}/approval", null)).EnsureSuccessStatusCode();
        return (client, playerId);
    }

    /// <summary>What authorization says to the operation, without running it.</summary>
    private static async Task<HttpStatusCode> AskAsync(HttpClient client, string operation, Dictionary<string, string> ids)
    {
        var parts = operation.Split(' ');
        var url = Placeholder().Replace(parts[1], match => ids.GetValueOrDefault(match.Groups[1].Value, Guid.NewGuid().ToString()));
        using var request = new HttpRequestMessage(new HttpMethod(parts[0]), url);
        request.Headers.Add(StopAfterAuthorization.Header, "1");
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static string Path(RouteEndpoint endpoint) =>
        "/" + Constraint().Replace(endpoint.RoutePattern.RawText!, "{$1}").Trim('/');

    private static string WhoMayCall(Endpoint endpoint)
    {
        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return Anonymous;
        }

        var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).Where(p => p is not null);
        return string.Join(" and ", policies) is { Length: > 0 } named ? named : SignedIn;
    }

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\{(\w+)(?::[^}]*)?\}")]
    private static partial Regex Constraint();

    /// <summary>The status that stands for "authorization let it through": no endpoint answers it.</summary>
    private const HttpStatusCode Allowed = (HttpStatusCode)299;

    /// <summary>Stops a marked request once authorization lets it through, so the handler's own answer can't be taken for a refusal.</summary>
    private sealed class StopAfterAuthorization(IAuthorizationMiddlewareResultHandler inner) : IAuthorizationMiddlewareResultHandler
    {
        public const string Header = "X-Stop-After-Authorization";

        public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        {
            if (!authorizeResult.Succeeded || !context.Request.Headers.ContainsKey(Header))
            {
                return inner.HandleAsync(next, context, policy, authorizeResult);
            }

            context.Response.StatusCode = (int)Allowed;
            return Task.CompletedTask;
        }
    }

    public sealed class Factory : AuthTestFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAuthorizationMiddlewareResultHandler>();
                services.AddSingleton<IAuthorizationMiddlewareResultHandler>(
                    new StopAfterAuthorization(new HidingAuthorizationResultHandler()));
            });
        }
    }
}
