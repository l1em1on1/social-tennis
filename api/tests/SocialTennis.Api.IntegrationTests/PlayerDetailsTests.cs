using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Features.Players.Contracts;
using SocialTennis.Api.Jobs;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// The rest of issue #4: a Player's Gender, Level and Rating, and Players a
/// Manager adds for people who haven't signed in yet.
/// </summary>
public class PlayerDetailsTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
{
    private static async Task<IReadOnlyList<PlayerSummary>> PlayersAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<GetPlayersResponse>("/players"))!.Players;

    private static async Task<Guid> CreatePlayerAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/players", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatePlayerResponse>())!.Id;
    }

    private static async Task<ClubMember> MemberAsync(HttpClient manager, Guid clubId, Guid playerId) =>
        (await manager.GetFromJsonAsync<GetClubMembersResponse>($"/clubs/{clubId}/members"))!
            .Members.Single(member => member.PlayerId == playerId);

    /// <summary>A Player in the factory's Club, so a Manager can see its Rating.</summary>
    private async Task<(HttpClient Client, Guid PlayerId, Guid ClubId)> PlayerInAClubAsync(object? body = null)
    {
        var client = await factory.CreateSignedInClientAsync();
        var playerId = await CreatePlayerAsync(client, body ?? new { name = "Sam" });
        var clubId = await factory.AClubIdAsync();
        (await factory.AskToJoinAsync(client, clubId, playerId)).EnsureSuccessStatusCode();
        return (client, playerId, clubId);
    }

    [Fact]
    public async Task A_Player_is_created_with_a_Gender_and_Level_or_with_just_a_name()
    {
        using var client = await factory.CreateSignedInClientAsync();

        await CreatePlayerAsync(client, new { name = "Sam", gender = "Female", level = 3 });
        await CreatePlayerAsync(client, new { name = "Alex" });

        var players = await PlayersAsync(client);
        Assert.Equal((PlayerGender.Female, 3), (players[0].Gender, players[0].Level));
        Assert.Equal((null, null), (players[1].Gender, players[1].Level));
    }

    [Fact]
    public async Task The_holding_User_edits_Name_Gender_and_Level_as_a_whole()
    {
        using var client = await factory.CreateSignedInClientAsync();
        var playerId = await CreatePlayerAsync(client, new { name = "Sam", gender = "Male", level = 2 });

        var edit = await client.PutAsJsonAsync($"/players/{playerId}", new { name = "Sam Smith", gender = "Female", level = 5 });
        Assert.Equal(HttpStatusCode.NoContent, edit.StatusCode);
        var edited = Assert.Single(await PlayersAsync(client));
        Assert.Equal(("Sam Smith", PlayerGender.Female, 5), (edited.Name, edited.Gender, edited.Level));

        // A field left out is cleared.
        await client.PutAsJsonAsync($"/players/{playerId}", new { name = "Sam Smith" });
        var cleared = Assert.Single(await PlayersAsync(client));
        Assert.Equal((null, null), (cleared.Gender, cleared.Level));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task A_Level_outside_1_to_5_is_refused(int level)
    {
        using var client = await factory.CreateSignedInClientAsync();
        var playerId = await CreatePlayerAsync(client, new { name = "Sam" });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/players", new { name = "Alex", level })).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.PutAsJsonAsync($"/players/{playerId}", new { name = "Sam", level })).StatusCode);
    }

    [Fact]
    public async Task A_Gender_that_is_not_one_of_the_two_is_refused()
    {
        using var client = await factory.CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("/players", new { name = "Sam", gender = "Sometimes" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await PlayersAsync(client));
    }

    [Fact]
    public async Task A_new_Players_Rating_is_1_whatever_its_Level()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var (client, playerId, clubId) = await PlayerInAClubAsync(new { name = "Sam", level = 5 });
        using var _ = client;

        Assert.Equal(1, (await MemberAsync(manager, clubId, playerId)).Rating);
    }

    [Fact]
    public async Task A_Manager_sets_a_Rating_and_nothing_the_holder_does_changes_it()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var (client, playerId, clubId) = await PlayerInAClubAsync();
        using var _ = client;

        var set = await manager.PutAsJsonAsync($"/players/{playerId}/rating", new { rating = 42 });

        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        Assert.Equal(42, (await MemberAsync(manager, clubId, playerId)).Rating);

        // The holder: not through the Rating route, and not by slipping it into an edit.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync($"/players/{playerId}/rating", new { rating = 99 })).StatusCode);
        await client.PutAsJsonAsync($"/players/{playerId}", new { name = "Sam", level = 4, rating = 99 });
        var member = await MemberAsync(manager, clubId, playerId);
        Assert.Equal(42, member.Rating);
        Assert.Equal(4, member.Level);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(10_001)]
    public async Task A_Rating_outside_its_range_is_refused(int rating)
    {
        using var manager = await factory.CreateManagerClientAsync();
        var (client, playerId, clubId) = await PlayerInAClubAsync();
        using var _ = client;

        var response = await manager.PutAsJsonAsync($"/players/{playerId}/rating", new { rating });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1, (await MemberAsync(manager, clubId, playerId)).Rating);
    }

    [Fact]
    public async Task Rating_a_Player_that_does_not_exist_is_not_found()
    {
        using var manager = await factory.CreateManagerClientAsync();

        var response = await manager.PutAsJsonAsync($"/players/{Guid.NewGuid()}/rating", new { rating = 5 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_Manager_adds_Players_for_an_email_and_whoever_signs_in_with_it_holds_them_all()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await factory.AClubIdAsync();
        var typed = $"Parent-{Guid.NewGuid():N}@Example.test";
        var email = typed.ToLowerInvariant();

        var first = await manager.PostAsJsonAsync($"/clubs/{clubId}/players", new { name = "  Alex Junior ", email = typed });
        var second = await manager.PostAsJsonAsync($"/clubs/{clubId}/players", new { name = "Sam Junior", email });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var added = (await first.Content.ReadFromJsonAsync<AddClubPlayerResponse>())!;
        Assert.Equal("Alex Junior", added.Name);
        var before = await MemberAsync(manager, clubId, added.PlayerId);
        Assert.Equal((email, false, true, 1), (before.Email, before.SignedIn, before.Approved, before.Rating));

        // Nobody has signed in with that address until now.
        using var parent = await factory.CreateSignedInClientAsync(email);

        var held = await PlayersAsync(parent);
        Assert.Equal(["Alex Junior", "Sam Junior"], held.Select(player => player.Name));
        Assert.All(held, player => Assert.True(Assert.Single(player.Clubs).Approved));
        Assert.True((await MemberAsync(manager, clubId, added.PlayerId)).SignedIn);

        // And from then on they are theirs to edit, like any other.
        var edit = await parent.PutAsJsonAsync($"/players/{added.PlayerId}", new { name = "Alex J", level = 2 });
        Assert.Equal(HttpStatusCode.NoContent, edit.StatusCode);
    }

    // #243 review: the nightly purge deletes ended sessions, and then a member read "Not signed in yet" again.
    [Fact]
    public async Task A_member_who_signed_out_has_still_signed_in_after_the_nightly_purge()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await factory.AClubIdAsync();
        var email = $"signed-out-{Guid.NewGuid():N}@example.test";
        var response = await manager.PostAsJsonAsync($"/clubs/{clubId}/players", new { name = "Out Again", email });
        var added = (await response.Content.ReadFromJsonAsync<AddClubPlayerResponse>())!;

        using var member = await factory.CreateSignedInClientAsync(email);
        (await member.DeleteAsync("/auth/sessions/current")).EnsureSuccessStatusCode();
        Assert.Equal(JobRunner.Succeeded, await JobRunner.RunAsync(factory.Services, "purge-tokens", CancellationToken.None));

        Assert.True((await MemberAsync(manager, clubId, added.PlayerId)).SignedIn);
    }

    // Signed in before LastSignedInAt was recorded (#117): the session says so.
    [Fact]
    public async Task A_member_signed_in_since_before_it_was_recorded_has_signed_in()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await factory.AClubIdAsync();
        var email = $"earlier-{Guid.NewGuid():N}@example.test";
        var response = await manager.PostAsJsonAsync($"/clubs/{clubId}/players", new { name = "Here Before", email });
        var added = (await response.Content.ReadFromJsonAsync<AddClubPlayerResponse>())!;

        using var member = await factory.CreateSignedInClientAsync(email);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
            await db.Users
                .Where(u => u.Email == email)
                .ExecuteUpdateAsync(set => set.SetProperty(u => u.LastSignedInAt, (DateTimeOffset?)null));
        }

        Assert.True((await MemberAsync(manager, clubId, added.PlayerId)).SignedIn);
    }

    [Fact]
    public async Task A_Player_added_for_someone_already_signed_in_is_theirs_at_once_and_lets_their_own_Players_in()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await factory.AClubIdAsync();
        var email = $"here-{Guid.NewGuid():N}@example.test";
        using var client = await factory.CreateSignedInClientAsync(email);
        var own = await CreatePlayerAsync(client, new { name = "Pat" });

        await manager.PostAsJsonAsync($"/clubs/{clubId}/players", new { name = "Pat Junior", email });

        // The login is let in, as pre-approving its email would (#224): Pat joins with Pat Junior.
        var players = await PlayersAsync(client);
        Assert.Equal(["Pat", "Pat Junior"], players.Select(player => player.Name));
        Assert.All(players, player => Assert.True(Assert.Single(player.Clubs, club => club.Id == clubId).Approved, player.Name));
        Assert.Contains(players, player => player.Id == own);
    }

    [Fact]
    public async Task Someone_else_signing_in_does_not_get_a_Player_added_for_another_email()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await factory.AClubIdAsync();
        await manager.PostAsJsonAsync(
            $"/clubs/{clubId}/players", new { name = "Not Yours", email = $"other-{Guid.NewGuid():N}@example.test" });

        using var stranger = await factory.CreateSignedInClientAsync();

        Assert.Empty(await PlayersAsync(stranger));
    }

    [Theory]
    [InlineData("", "someone@example.test")]
    [InlineData("Sam", "")]
    [InlineData("Sam", "not-an-email")]
    public async Task Adding_a_Player_needs_a_name_and_an_email(string name, string email)
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await factory.AClubIdAsync();

        var response = await manager.PostAsJsonAsync($"/clubs/{clubId}/players", new { name, email });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Only_a_Manager_adds_Players_and_only_to_a_Club_that_exists()
    {
        using var player = await factory.CreateSignedInClientAsync();
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await factory.AClubIdAsync();
        var body = new { name = "Sam", email = "someone@example.test" };

        Assert.Equal(HttpStatusCode.Forbidden, (await player.PostAsJsonAsync($"/clubs/{clubId}/players", body)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await manager.PostAsJsonAsync($"/clubs/{Guid.NewGuid()}/players", body)).StatusCode);
    }

    [Fact]
    public async Task A_Player_can_belong_to_more_than_one_Club_at_once()
    {
        var admin = await factory.AdminClientAsync();
        using var client = await factory.CreateSignedInClientAsync();
        var playerId = await CreatePlayerAsync(client, new { name = "Sam" });
        var firstClub = await factory.AClubIdAsync();
        var secondClub = (await (await admin.PostAsJsonAsync("/clubs", new { name = $"Second {Guid.NewGuid():N}" }))
            .Content.ReadFromJsonAsync<CreateClubResponse>())!.Id;

        await factory.AskToJoinAsync(client, firstClub, playerId);
        await factory.AskToJoinAsync(client, secondClub, playerId);
        await admin.PutAsync($"/clubs/{firstClub}/members/{playerId}/approval", null);
        await admin.PutAsync($"/clubs/{secondClub}/members/{playerId}/approval", null);

        var clubs = Assert.Single(await PlayersAsync(client)).Clubs;
        Assert.Equal(new[] { firstClub, secondClub }.Order(), clubs.Select(club => club.Id).Order());
        Assert.All(clubs, club => Assert.True(club.Approved));
    }
}
