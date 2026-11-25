using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Features.Players.Contracts;
using SocialTennis.Api.Membership;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// A Club's join link (#222, ADR-0032): one shared link per Club, which a
/// Manager turns on, resets and turns off. Opening it lets a Player ask to
/// join; a Manager still approves.
/// </summary>
public class JoinLinkTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
{
    /// <summary>A Club of its own, so resetting its link touches no other test's.</summary>
    private async Task<Guid> NewClubAsync(params HttpClient[] managers)
    {
        var admin = await factory.AdminClientAsync();
        var created = await admin.PostAsJsonAsync("/clubs", new { name = $"Linked {Guid.NewGuid():N}" });
        var id = (await created.Content.ReadFromJsonAsync<CreateClubResponse>())!.Id;
        foreach (var manager in managers)
        {
            await factory.MakeManagerAsync(manager, id);
        }

        return id;
    }

    private static async Task<Guid> PlayerAsync(HttpClient client, string name) =>
        (await (await client.PostAsJsonAsync("/players", new { name })).Content.ReadFromJsonAsync<CreatePlayerResponse>())!.Id;

    private static async Task<string?> CodeAsync(HttpClient manager, Guid clubId) =>
        (await manager.GetFromJsonAsync<ClubJoinLink>($"/clubs/{clubId}/join-link"))!.Code;

    private static async Task<string> TurnOnAsync(HttpClient manager, Guid clubId)
    {
        var response = await manager.PutAsync($"/clubs/{clubId}/join-link", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ClubJoinLink>())!.Code!;
    }

    private static async Task<string> ResetAsync(HttpClient manager, Guid clubId)
    {
        var response = await manager.PostAsync($"/clubs/{clubId}/join-link/reset", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ClubJoinLink>())!.Code!;
    }

    private static Task<HttpResponseMessage> AskAsync(HttpClient client, string code, Guid playerId) =>
        client.PutAsync($"/join-links/{code}/members/{playerId}", null);

    private static async Task<ClubMember?> MemberAsync(HttpClient manager, Guid clubId, Guid playerId) =>
        (await manager.GetFromJsonAsync<GetClubMembersResponse>($"/clubs/{clubId}/members"))!.Members
            .SingleOrDefault(m => m.PlayerId == playerId);

    [Fact]
    public async Task A_Player_asks_through_the_link_and_waits_for_a_Manager()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        // Every Club's link starts off.
        Assert.Null(await CodeAsync(manager, clubId));
        var code = await TurnOnAsync(manager, clubId);
        Assert.Equal(code, await CodeAsync(manager, clubId));
        Assert.True(JoinCodes.IsWellFormed(code));
        // Turning it on again keeps the link.
        Assert.Equal(code, await TurnOnAsync(manager, clubId));

        using var client = await factory.CreateSignedInClientAsync();
        var playerId = await PlayerAsync(client, "Sam");
        var club = await client.GetFromJsonAsync<GetJoinLinkClubResponse>($"/join-links/{code}");
        Assert.Equal(clubId, club!.ClubId);
        Assert.StartsWith("Linked ", club.ClubName, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NoContent, (await AskAsync(client, code, playerId)).StatusCode);
        Assert.False((await MemberAsync(manager, clubId, playerId))!.Approved);
        (await manager.PutAsync($"/clubs/{clubId}/members/{playerId}/approval", null)).EnsureSuccessStatusCode();
        Assert.True((await MemberAsync(manager, clubId, playerId))!.Approved);
    }

    [Fact]
    public async Task A_reset_link_stops_working_and_requests_made_through_it_stay()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var old = await TurnOnAsync(manager, clubId);
        using var client = await factory.CreateSignedInClientAsync();
        var asked = await PlayerAsync(client, "Asked before");
        (await AskAsync(client, old, asked)).EnsureSuccessStatusCode();

        var fresh = await ResetAsync(manager, clubId);

        Assert.NotEqual(old, fresh);
        Assert.Equal(fresh, await CodeAsync(manager, clubId));
        var late = await PlayerAsync(client, "Asked after");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/join-links/{old}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AskAsync(client, old, late)).StatusCode);
        Assert.Null(await MemberAsync(manager, clubId, late));
        Assert.False((await MemberAsync(manager, clubId, asked))!.Approved);
        Assert.Equal(HttpStatusCode.NoContent, (await AskAsync(client, fresh, late)).StatusCode);
    }

    [Fact]
    public async Task A_link_turned_off_stops_working_and_turning_it_on_again_gives_a_new_one()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);
        var old = await TurnOnAsync(manager, clubId);

        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync($"/clubs/{clubId}/join-link")).StatusCode);
        // Turning off a link that's off is fine.
        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync($"/clubs/{clubId}/join-link")).StatusCode);

        Assert.Null(await CodeAsync(manager, clubId));
        using var client = await factory.CreateSignedInClientAsync();
        var playerId = await PlayerAsync(client, "Sam");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/join-links/{old}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AskAsync(client, old, playerId)).StatusCode);
        Assert.NotEqual(old, await TurnOnAsync(manager, clubId));
    }

    [Fact]
    public async Task Managers_turning_a_link_on_at_once_get_one_link()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync(manager);

        var codes = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => TurnOnAsync(manager, clubId)));

        Assert.Single(codes.Distinct());
        Assert.Equal(codes[0], await CodeAsync(manager, clubId));
    }

    [Fact]
    public async Task A_link_off_reset_or_unknown_answers_the_same()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var resetClub = await NewClubAsync(manager);
        var reset = await TurnOnAsync(manager, resetClub);
        await ResetAsync(manager, resetClub);
        var offClub = await NewClubAsync(manager);
        var off = await TurnOnAsync(manager, offClub);
        (await manager.DeleteAsync($"/clubs/{offClub}/join-link")).EnsureSuccessStatusCode();
        using var client = await factory.CreateSignedInClientAsync();

        var bodies = new List<string>();
        foreach (var code in new[] { reset, off, JoinCodes.New(), "not-a-code" })
        {
            var response = await client.GetAsync($"/join-links/{code}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            // The same problem each time, but for its trace id.
            bodies.Add(System.Text.RegularExpressions.Regex.Replace(await response.Content.ReadAsStringAsync(), "\"traceId\":\"[^\"]*\"", string.Empty));
        }

        Assert.Single(bodies.Distinct());
    }

    [Fact]
    public async Task Only_a_Manager_reads_resets_or_turns_off_a_link()
    {
        var clubId = await NewClubAsync();
        // Neither someone outside the Club nor another Club's manager may know it is there (#110).
        using var client = await factory.CreateSignedInClientAsync();
        using var elsewhere = await factory.CreateManagerClientAsync();
        foreach (var outsider in new[] { client, elsewhere })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/clubs/{clubId}/join-link")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.PutAsync($"/clubs/{clubId}/join-link", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsync($"/clubs/{clubId}/join-link/reset", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.DeleteAsync($"/clubs/{clubId}/join-link")).StatusCode);
        }

        using var manager = await factory.CreateManagerClientAsync(clubId);
        Assert.Null(await CodeAsync(manager, clubId));
        // A Club that doesn't exist has no link.
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/clubs/{Guid.NewGuid()}/join-link")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PutAsync($"/clubs/{Guid.NewGuid()}/join-link", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsync($"/clubs/{Guid.NewGuid()}/join-link/reset", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.DeleteAsync($"/clubs/{Guid.NewGuid()}/join-link")).StatusCode);
    }

    [Fact]
    public async Task A_join_link_needs_a_session()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var code = await TurnOnAsync(manager, await NewClubAsync(manager));
        using var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/join-links/{code}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AskAsync(anonymous, code, Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task Signing_in_from_a_join_link_carries_its_code_back_in_the_emailed_link()
    {
        using var client = factory.CreateClient();
        var code = JoinCodes.New();
        var email = $"joiner-{Guid.NewGuid():N}@example.test";

        (await client.PostAsJsonAsync("/auth/magic-link", new { email, join = code })).EnsureSuccessStatusCode();

        var link = factory.Sender.LinkFor(email);
        Assert.Equal("http://localhost:3000/auth/verify", link.GetLeftPart(UriPartial.Path));
        Assert.EndsWith($"&join={code}", link.Query, StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(factory.Sender.TokenFor(email)));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("../../admin?x=1&y=2____")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaa=")]
    // A code and a newline: .NET's $ would match before it (#223 review).
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA\n")]
    public async Task A_sign_in_request_with_a_code_of_the_wrong_shape_is_refused(string join)
    {
        using var client = factory.CreateClient();
        var email = $"joiner-{Guid.NewGuid():N}@example.test";

        var response = await client.PostAsJsonAsync("/auth/magic-link", new { email, join });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // Under its wire name, as every refusal is (ADR-0017, #223 review).
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["join"], problem!.Errors.Keys);
        Assert.Equal(0, factory.Sender.SentTo(email));
    }
}
