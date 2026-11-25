using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Auth.Contracts;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Features.Leagues.Contracts;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// An Admin deletes a User (#116, ADR-0024): their Players become Former
/// players, results intact, and the User leaves no trace. Here because it
/// needs a League with results, which these tests build.
/// </summary>
public partial class ScoreTests
{
    private static async Task<Guid> UserIdAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!.UserId;

    private async Task<HttpResponseMessage> DeleteUserAsync(Guid userId) =>
        await (await factory.AdminClientAsync()).DeleteAsync($"/users/{userId}");

    [Fact]
    public async Task Deleting_a_User_makes_their_Players_Former_players_and_keeps_every_result()
    {
        var game = await SinglesAsync(OneSetOf6);
        (await SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 2)])).EnsureSuccessStatusCode();
        var before = await LadderOfAsync(game.Vic.Client, game.LeagueId);

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteUserAsync(await UserIdAsync(game.Una.Client))).StatusCode);

        // Una's session went with her.
        Assert.Equal(HttpStatusCode.Unauthorized, (await game.Una.Client.GetAsync("/players")).StatusCode);
        // The Game, its Score and its history read "Former player"; the Standings are as they were.
        var seen = await GameAsync(game.Vic.Client, game.GameId);
        Assert.Equal(Player.FormerPlayerName, seen.Sides.Single(s => s.Side == 1).Players.Single().Name);
        Assert.Equal((1, Player.FormerPlayerName), (seen.Score!.Winner, seen.Score.SubmittedBy));
        Assert.Equal(Player.FormerPlayerName, Assert.Single(seen.History).By);
        var after = await LadderOfAsync(game.Vic.Client, game.LeagueId);
        Assert.Equal(
            before.Divisions[0].Standings.Select(s => (s.TeamId, s.Rank, s.Won, s.Lost)),
            after.Divisions[0].Standings.Select(s => (s.TeamId, s.Rank, s.Won, s.Lost)));
        // Still in the League, with no email, and no longer a member of the Club.
        var league = (await game.Manager.GetFromJsonAsync<GetLeagueResponse>($"/leagues/{game.LeagueId}"))!;
        var former = league.Participants.Single(p => p.PlayerId == game.Una.PlayerId);
        Assert.Equal((Player.FormerPlayerName, (string?)null, (PlayerGender?)null), (former.Name, former.Email, former.Gender));
        var members = (await game.Manager.GetFromJsonAsync<GetClubMembersResponse>($"/clubs/{game.ClubId}/members"))!;
        Assert.DoesNotContain(members.Members, m => m.PlayerId == game.Una.PlayerId);
    }

    [Fact]
    public async Task A_deleted_Managers_override_reads_as_a_manager_and_names_no_Former_player()
    {
        var game = await SinglesAsync(OneSetOf6);
        (await SubmitAsync(game.Una.Client, game.GameId, game.Una.PlayerId, [Set(6, 2)])).EnsureSuccessStatusCode();
        // A Manager with a Player of their own, so the override is named by it.
        using var manager = await factory.CreateManagerClientAsync(game.ClubId);
        (await manager.PostAsJsonAsync("/players", new { name = "Morgan Manager" })).EnsureSuccessStatusCode();
        var version = (await GameAsync(manager, game.GameId)).Score!.Version;
        (await OverrideAsync(manager, game.GameId, version, [Set(2, 6)])).EnsureSuccessStatusCode();
        Assert.Equal("Morgan Manager", (await GameAsync(game.Vic.Client, game.GameId)).History[0].By);

        (await DeleteUserAsync(await UserIdAsync(manager))).EnsureSuccessStatusCode();

        // The Manager's Player is a Former player now, but the override isn't theirs as a Player:
        // it names nobody, which the page shows as "A manager".
        var seen = await GameAsync(game.Vic.Client, game.GameId);
        Assert.Null(seen.History[0].By);
        Assert.Equal("Una", seen.History[1].By);
    }

    [Fact]
    public async Task A_deleted_Manager_who_marked_a_Game_not_played_is_deleted_and_the_mark_stays()
    {
        // The mark points at its Manager under a Restrict key: left in place, it would block the delete (#228 review).
        var game = await SinglesAsync(OneSetOf6);
        using var manager = await factory.CreateManagerClientAsync(game.ClubId);
        (await VoidAsync(manager, game.GameId)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteUserAsync(await UserIdAsync(manager))).StatusCode);

        Assert.True((await GameAsync(game.Vic.Client, game.GameId)).NotPlayed);
    }

    [Fact]
    public async Task A_deleted_email_signs_up_again_empty_and_its_old_links_and_invites_are_gone()
    {
        var email = $"forgotten-{Guid.NewGuid():N}@example.test";
        using var client = await factory.CreateSignedInClientAsync(email);
        (await client.PostAsJsonAsync("/players", new { name = "Fran" })).EnsureSuccessStatusCode();
        var oldUser = await UserIdAsync(client);
        // A sign-in link not yet used, and an invite waiting for the email at a second Club.
        using var anonymous = factory.CreateClient();
        (await anonymous.PostAsJsonAsync("/auth/magic-link", new { email })).EnsureSuccessStatusCode();
        var unused = factory.Sender.TokenFor(email);
        var (manager, otherClub) = await ClubAsync();
        (await manager.PostAsJsonAsync($"/clubs/{otherClub}/invites", new { email })).EnsureSuccessStatusCode();

        (await DeleteUserAsync(oldUser)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/auth/sessions", new { token = unused })).StatusCode);
        var invites = (await manager.GetFromJsonAsync<GetClubMembersResponse>($"/clubs/{otherClub}/members"))!.Invites;
        Assert.DoesNotContain(invites, i => i.Email == email);
        using var again = await factory.CreateSignedInClientAsync(email);
        Assert.NotEqual(oldUser, await UserIdAsync(again));
        Assert.Empty((await again.GetFromJsonAsync<GetPlayersResponse>("/players"))!.Players);
    }

    [Fact]
    public async Task A_waiting_request_naming_a_deleted_Player_as_partner_is_withdrawn()
    {
        var (manager, clubId) = await ClubAsync();
        var leagueId = await LeagueAsync(manager, clubId, OneSetOf6, "DoublesMix");
        var ann = await MemberAsync(manager, clubId, "Ann");
        var bob = await MemberAsync(manager, clubId, "Bob");
        (await ann.Client.PostAsJsonAsync($"/leagues/{leagueId}/join-requests", new { playerId = ann.PlayerId, partnerId = bob.PlayerId }))
            .EnsureSuccessStatusCode();

        (await DeleteUserAsync(await UserIdAsync(bob.Client))).EnsureSuccessStatusCode();

        var league = (await manager.GetFromJsonAsync<GetLeagueResponse>($"/leagues/{leagueId}"))!;
        Assert.DoesNotContain(league.JoinRequests, r => r.PlayerId == ann.PlayerId);
    }

    /// <summary>
    /// Deleting a User, as asking sees it: the email's admission lock held while
    /// their Player becomes a Former player and leaves the Club. A request made
    /// meanwhile waits for the lock, then finds the Player held by nobody
    /// (#228 review).
    /// </summary>
    private async Task<HttpResponseMessage> AskWhileDeletingAsync(string email, Guid playerId, Func<Task<HttpResponseMessage>> ask)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
        await using var deleting = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(1160001, hashtext({email}))");
        var asking = ask();
        await Task.Delay(500);
        await db.Players
            .Where(p => p.Id == playerId)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.UserId, (Guid?)null).SetProperty(p => p.Name, Player.FormerPlayerName));
        await db.ClubMemberships.Where(m => m.PlayerId == playerId).ExecuteDeleteAsync();
        await deleting.CommitAsync();
        return await asking;
    }

    /// <summary>Whether anything still lets the Player in: a membership, a League request, or a place in a League.</summary>
    private async Task<bool> AnyEntryOfAsync(Guid playerId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
        return await db.ClubMemberships.AnyAsync(m => m.PlayerId == playerId)
            || await db.LeagueJoinRequests.AnyAsync(r => r.PlayerId == playerId)
            || await db.LeagueParticipants.AnyAsync(p => p.PlayerId == playerId);
    }

    [Fact]
    public async Task Asking_to_join_a_Club_while_the_User_is_deleted_leaves_no_request()
    {
        var (_, clubId) = await ClubAsync();
        var email = $"racing-{Guid.NewGuid():N}@example.test";
        using var client = await factory.CreateSignedInClientAsync(email);
        var playerId = (await (await client.PostAsJsonAsync("/players", new { name = "Rae" })).Content.ReadFromJsonAsync<CreatePlayerResponse>())!.Id;
        var code = await factory.JoinCodeAsync(clubId);

        var asked = await AskWhileDeletingAsync(email, playerId, () => client.PutAsync($"/join-links/{code}/members/{playerId}", null));

        Assert.Equal(HttpStatusCode.NotFound, asked.StatusCode);
        Assert.False(await AnyEntryOfAsync(playerId));
    }

    [Fact]
    public async Task Asking_to_join_a_League_while_the_User_is_deleted_leaves_no_request()
    {
        var (manager, clubId) = await ClubAsync();
        var leagueId = await LeagueAsync(manager, clubId, OneSetOf6);
        var ann = await MemberAsync(manager, clubId, "Ann");
        var email = (await ann.Client.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!.Email;

        var asked = await AskWhileDeletingAsync(
            email, ann.PlayerId, () => ann.Client.PostAsJsonAsync($"/leagues/{leagueId}/join-requests", new { playerId = ann.PlayerId }));

        Assert.Equal(HttpStatusCode.NotFound, asked.StatusCode);
        Assert.False(await AnyEntryOfAsync(ann.PlayerId));
    }

    [Fact]
    public async Task A_Manager_adding_a_Player_to_a_League_while_their_User_is_deleted_adds_no_Former_player()
    {
        var (manager, clubId) = await ClubAsync();
        var leagueId = await LeagueAsync(manager, clubId, OneSetOf6);
        var ann = await MemberAsync(manager, clubId, "Ann");
        var email = (await ann.Client.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!.Email;

        var added = await AskWhileDeletingAsync(email, ann.PlayerId, () => manager.PutAsync($"/leagues/{leagueId}/participants/{ann.PlayerId}", null));

        // In no Club once deleted, so not addable (#228 review).
        Assert.Equal(HttpStatusCode.Conflict, added.StatusCode);
        Assert.False(await AnyEntryOfAsync(ann.PlayerId));
    }

    [Fact]
    public async Task An_Admin_cant_delete_themselves_or_an_Admin_by_configuration_and_only_an_Admin_deletes()
    {
        var admin = await factory.AdminClientAsync();
        var own = await admin.DeleteAsync($"/users/{await UserIdAsync(admin)}");
        Assert.Equal("own-account", await ProblemTypeAsync(own, HttpStatusCode.Conflict));
        // Listed in Auth:AdminEmails beside the factory's Admin.
        using var configured = await factory.CreateSignedInClientAsync("someone@example.test");
        Assert.Equal("admin-by-setting", await ProblemTypeAsync(await DeleteUserAsync(await UserIdAsync(configured)), HttpStatusCode.Conflict));
        Assert.Equal(HttpStatusCode.NotFound, (await DeleteUserAsync(Guid.NewGuid())).StatusCode);

        using var manager = await factory.CreateManagerClientAsync();
        using var target = await factory.CreateSignedInClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.DeleteAsync($"/users/{await UserIdAsync(target)}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await target.GetAsync("/players")).StatusCode);
    }
}
