using System.Net;
using System.Net.Http.Json;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Auth.Contracts;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Features.Leagues.Contracts;
using SocialTennis.Api.Features.Users.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// A manager belongs to a Club, or several (#110, ADR-0034). Who may call which
/// route is <see cref="ManagerAccessTests"/>'; this is what a manager gets.
/// </summary>
public class ClubManagersTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
{
    private async Task<Guid> ClubAsync(string name)
    {
        var admin = await factory.AdminClientAsync();
        var created = await admin.PostAsJsonAsync("/clubs", new { name = $"{name} {Guid.NewGuid():N}" });
        return (await created.Content.ReadFromJsonAsync<CreateClubResponse>())!.Id;
    }

    private static async Task<Guid> LeagueAsync(HttpClient manager, Guid clubId)
    {
        var created = await manager.PostAsJsonAsync($"/clubs/{clubId}/leagues", new
        {
            name = "Winter Singles",
            type = "SinglesMixed",
            structure = "Divisional",
            startDate = "2026-12-01",
            endDate = "2027-03-31",
            stageLength = new { count = 1, unit = "Months" },
            stageRoll = "Manual",
            scoreFormat = new { gamesPerSet = 6, setsPerGame = 1, matchTiebreakDecider = false },
        });
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<CreateLeagueResponse>())!.Id;
    }

    private static async Task<IEnumerable<Guid>> ClubIdsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<GetClubsResponse>("/clubs"))!.Clubs.Select(c => c.Id).Order();

    private static async Task<GetCurrentUserResponse> MeAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!;

    [Fact]
    public async Task A_manager_runs_their_Club_and_another_Club_is_hidden_from_them()
    {
        var (ours, theirs) = (await ClubAsync("Ours"), await ClubAsync("Theirs"));
        using var manager = await factory.CreateManagerClientAsync(ours);
        using var other = await factory.CreateManagerClientAsync(theirs);
        var ourLeague = await LeagueAsync(manager, ours);
        var theirLeague = await LeagueAsync(other, theirs);

        Assert.Equal([ours], await ClubIdsAsync(manager));
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/leagues/{ourLeague}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/clubs/{theirs}/members")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/leagues/{theirLeague}")).StatusCode);
        // A route for Players and managers both asks the same rule.
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/leagues/{ourLeague}/ladder")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/leagues/{theirLeague}/ladder")).StatusCode);
    }

    [Fact]
    public async Task A_User_can_manage_several_Clubs_and_lose_one_of_them()
    {
        var (first, second) = (await ClubAsync("First"), await ClubAsync("Second"));
        using var manager = await factory.CreateManagerClientAsync(first, second);
        Assert.Equal(new[] { first, second }.Order(), await ClubIdsAsync(manager));
        var me = await MeAsync(manager);
        var admin = await factory.AdminClientAsync();
        var listed = (await admin.GetFromJsonAsync<GetUsersResponse>($"/users?q={Uri.EscapeDataString(me.Email)}"))!.Users.Single();
        Assert.Equal(
            new[] { first, second }.Order(),
            listed.Clubs.Where(c => c.Role == ClubRoleKind.Manager).Select(c => c.ClubId).Order());

        (await admin.DeleteAsync($"/clubs/{first}/managers/{me.UserId}")).EnsureSuccessStatusCode();

        Assert.Equal([second], await ClubIdsAsync(manager));
        Assert.True((await MeAsync(manager)).Manages);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/clubs/{first}/members")).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_User_takes_their_roles_and_the_email_comes_back_without_them()
    {
        var club = await ClubAsync("Leaving");
        var email = $"leaving-{Guid.NewGuid():N}@example.test";
        using var manager = await factory.CreateSignedInClientAsync(email);
        await factory.MakeManagerAsync(manager, club);
        var me = await MeAsync(manager);
        var admin = await factory.AdminClientAsync();

        (await admin.DeleteAsync($"/users/{me.UserId}")).EnsureSuccessStatusCode();

        using var again = await factory.CreateSignedInClientAsync(email);
        Assert.False((await MeAsync(again)).Manages);
        Assert.Equal(HttpStatusCode.NotFound, (await again.GetAsync($"/clubs/{club}/members")).StatusCode);
    }

    [Fact]
    public async Task The_last_manager_of_a_Club_can_go_and_an_Admin_still_runs_it()
    {
        var club = await ClubAsync("Unmanaged");
        using var manager = await factory.CreateManagerClientAsync(club);
        var admin = await factory.AdminClientAsync();

        (await admin.DeleteAsync($"/clubs/{club}/managers/{(await MeAsync(manager)).UserId}")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/clubs/{club}/members")).StatusCode);
        Assert.Contains(club, await ClubIdsAsync(admin));
    }
}
