using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Auth.Contracts;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// A User's own Players and their Club membership, over HTTP against the real
/// Postgres. Every test signs in as a fresh User, so they don't see each other.
/// </summary>
public class PlayersAndMembershipTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
{
    private static async Task<CreatePlayerResponse> CreatePlayerAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/players", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        // No route reads a single Player, so there is nothing to point at.
        Assert.Null(response.Headers.Location);
        return (await response.Content.ReadFromJsonAsync<CreatePlayerResponse>())!;
    }

    private static async Task<IReadOnlyList<PlayerSummary>> PlayersAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<GetPlayersResponse>("/players"))!.Players;

    private static async Task<GetClubMembersResponse> MembersAsync(HttpClient manager, Guid clubId) =>
        (await manager.GetFromJsonAsync<GetClubMembersResponse>($"/clubs/{clubId}/members"))!;

    private static async Task<IReadOnlyList<ClubInviteSummary>> InvitesForAsync(HttpClient manager, Guid clubId, string email) =>
        (await MembersAsync(manager, clubId)).Invites.Where(i => i.Email == email).ToList();

    /// <summary>A second Club, for the rules that reach across Clubs.</summary>
    private async Task<Guid> NewClubIdAsync()
    {
        var admin = await factory.AdminClientAsync();
        var created = await admin.PostAsJsonAsync("/clubs", new { name = $"Other Club {Guid.NewGuid():N}" });
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<CreateClubResponse>())!.Id;
    }

    [Fact]
    public async Task A_new_User_holds_no_Player()
    {
        using var client = await factory.CreateSignedInClientAsync();

        var body = await client.GetFromJsonAsync<GetPlayersResponse>("/players");

        Assert.Empty(body!.Players);
        Assert.Equal(0, body.Page.Total);
    }

    [Fact]
    public async Task A_User_can_hold_several_Players_and_sees_them_oldest_first()
    {
        using var client = await factory.CreateSignedInClientAsync();

        await CreatePlayerAsync(client, "  Pat Parent  ");
        await CreatePlayerAsync(client, "Alex Junior");

        var players = await PlayersAsync(client);
        Assert.Equal(["Pat Parent", "Alex Junior"], players.Select(p => p.Name));
        Assert.All(players, p => Assert.Empty(p.Clubs));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_Player_needs_a_name(string name)
    {
        using var client = await factory.CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("/players", new { name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await PlayersAsync(client));
    }

    [Fact]
    public async Task A_name_longer_than_100_characters_is_refused()
    {
        using var client = await factory.CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("/players", new { name = new string('a', 101) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_holding_User_can_rename_a_Player()
    {
        using var client = await factory.CreateSignedInClientAsync();
        var player = await CreatePlayerAsync(client, "Sam");

        var response = await client.PutAsJsonAsync($"/players/{player.Id}", new { name = "Sam Smith" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Sam Smith", Assert.Single(await PlayersAsync(client)).Name);
    }

    [Fact]
    public async Task Another_Users_Player_cannot_be_seen_renamed_or_joined_to_a_Club()
    {
        using var owner = await factory.CreateSignedInClientAsync();
        using var other = await factory.CreateSignedInClientAsync();
        var player = await CreatePlayerAsync(owner, "Sam");
        var clubId = await factory.AClubIdAsync();

        var rename = await other.PutAsJsonAsync($"/players/{player.Id}", new { name = "Hijacked" });
        var join = await other.PutAsync($"/clubs/{clubId}/members/{player.Id}", null);

        Assert.Equal(HttpStatusCode.NotFound, rename.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, join.StatusCode);
        Assert.Empty(await PlayersAsync(other));
        var unchanged = Assert.Single(await PlayersAsync(owner));
        Assert.Equal("Sam", unchanged.Name);
        Assert.Empty(unchanged.Clubs);
    }

    [Fact]
    public async Task Asking_to_join_waits_for_a_Manager_and_approval_makes_the_Player_a_member()
    {
        using var client = await factory.CreateSignedInClientAsync();
        using var manager = await factory.CreateManagerClientAsync();
        var player = await CreatePlayerAsync(client, "Sam");
        var sibling = await CreatePlayerAsync(client, "Alex");
        var clubId = await factory.AClubIdAsync();

        var ask = await client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null);

        Assert.Equal(HttpStatusCode.NoContent, ask.StatusCode);
        var waiting = Assert.Single((await PlayersAsync(client)).Single(p => p.Id == player.Id).Clubs);
        Assert.False(waiting.Approved);
        var request = (await MembersAsync(manager, clubId)).Members.Single(m => m.PlayerId == player.Id);
        Assert.False(request.Approved);
        Assert.Equal("Sam", request.Name);

        var approve = await manager.PutAsync($"/clubs/{clubId}/members/{player.Id}/approval", null);

        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);
        var players = await PlayersAsync(client);
        Assert.True(Assert.Single(players.Single(p => p.Id == player.Id).Clubs).Approved);
        Assert.Empty(players.Single(p => p.Id == sibling.Id).Clubs);
        Assert.True((await MembersAsync(manager, clubId)).Members.Single(m => m.PlayerId == player.Id).Approved);
    }

    [Fact]
    public async Task Asking_again_changes_nothing_and_never_undoes_an_approval()
    {
        using var client = await factory.CreateSignedInClientAsync();
        using var manager = await factory.CreateManagerClientAsync();
        var player = await CreatePlayerAsync(client, "Sam");
        var clubId = await factory.AClubIdAsync();

        var presses = await Task.WhenAll(
            client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null),
            client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null),
            client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null));
        Assert.All(presses, press => Assert.Equal(HttpStatusCode.NoContent, press.StatusCode));
        Assert.False(Assert.Single(Assert.Single(await PlayersAsync(client)).Clubs).Approved);

        await manager.PutAsync($"/clubs/{clubId}/members/{player.Id}/approval", null);
        await client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null);

        Assert.True(Assert.Single(Assert.Single(await PlayersAsync(client)).Clubs).Approved);
    }

    [Fact]
    public async Task A_Manager_can_decline_a_request_and_the_Player_can_ask_again()
    {
        using var client = await factory.CreateSignedInClientAsync();
        using var manager = await factory.CreateManagerClientAsync();
        var player = await CreatePlayerAsync(client, "Sam");
        var clubId = await factory.AClubIdAsync();
        await client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null);

        var decline = await manager.DeleteAsync($"/clubs/{clubId}/members/{player.Id}");

        Assert.Equal(HttpStatusCode.NoContent, decline.StatusCode);
        Assert.Empty(Assert.Single(await PlayersAsync(client)).Clubs);
        Assert.DoesNotContain((await MembersAsync(manager, clubId)).Members, m => m.PlayerId == player.Id);

        await client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null);
        Assert.False(Assert.Single(Assert.Single(await PlayersAsync(client)).Clubs).Approved);
    }

    [Fact]
    public async Task An_invite_lets_in_its_Users_first_Player_and_is_used_up_and_later_Players_follow()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var typed = $"Invited-{Guid.NewGuid():N}@Example.test";
        var email = typed.ToLowerInvariant();
        var clubId = await factory.AClubIdAsync();

        var invite = await manager.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email = $"  {typed} " });
        var again = await manager.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email });

        Assert.Equal(HttpStatusCode.NoContent, invite.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Single(await InvitesForAsync(manager, clubId, email));

        using var invited = await factory.CreateSignedInClientAsync(email);
        await CreatePlayerAsync(invited, "Pat");
        Assert.Empty(await InvitesForAsync(manager, clubId, email));
        await CreatePlayerAsync(invited, "Pat Junior");

        Assert.All(await PlayersAsync(invited), p => Assert.True(Assert.Single(p.Clubs).Approved));
    }

    [Fact]
    public async Task Inviting_an_email_whose_User_already_has_Players_lets_them_in_including_a_waiting_request()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var email = $"existing-{Guid.NewGuid():N}@example.test";
        using var client = await factory.CreateSignedInClientAsync(email);
        var asked = await CreatePlayerAsync(client, "Sam");
        await CreatePlayerAsync(client, "Alex");
        var clubId = await factory.AClubIdAsync();
        await client.PutAsync($"/clubs/{clubId}/members/{asked.Id}", null);

        var invite = await manager.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email });

        Assert.Equal(HttpStatusCode.NoContent, invite.StatusCode);
        Assert.All(await PlayersAsync(client), p => Assert.True(Assert.Single(p.Clubs).Approved));
        Assert.Empty(await InvitesForAsync(manager, clubId, email));
    }

    [Fact]
    public async Task A_removed_member_asks_again_and_waits_and_later_Players_do_not_follow()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var email = $"removed-{Guid.NewGuid():N}@example.test";
        var clubId = await factory.AClubIdAsync();
        await manager.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email });
        using var client = await factory.CreateSignedInClientAsync(email);
        var player = await CreatePlayerAsync(client, "Bob");

        var remove = await manager.DeleteAsync($"/clubs/{clubId}/members/{player.Id}");

        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        Assert.DoesNotContain((await MembersAsync(manager, clubId)).Invites, i => i.Email == email);
        await client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null);
        Assert.False(Assert.Single(Assert.Single(await PlayersAsync(client)).Clubs).Approved);
        var later = await CreatePlayerAsync(client, "Bob Junior");
        Assert.Empty((await PlayersAsync(client)).Single(p => p.Id == later.Id).Clubs);
    }

    [Fact]
    public async Task Withdrawing_an_unused_invite_admits_nobody()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var email = $"withdrawn-{Guid.NewGuid():N}@example.test";
        var clubId = await factory.AClubIdAsync();
        await manager.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email });
        var invite = Assert.Single(await InvitesForAsync(manager, clubId, email));

        var withdraw = await manager.DeleteAsync($"/clubs/{clubId}/invites/{invite.Id}");
        var again = await manager.DeleteAsync($"/clubs/{clubId}/invites/{invite.Id}");

        Assert.Equal(HttpStatusCode.NoContent, withdraw.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Empty(await InvitesForAsync(manager, clubId, email));
        using var client = await factory.CreateSignedInClientAsync(email);
        await CreatePlayerAsync(client, "Late");
        Assert.Empty(Assert.Single(await PlayersAsync(client)).Clubs);
    }

    [Fact]
    public async Task An_invite_of_another_Club_is_not_withdrawn_through_this_one()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var email = $"kept-{Guid.NewGuid():N}@example.test";
        var clubId = await factory.AClubIdAsync();
        await manager.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email });
        var invite = (await MembersAsync(manager, clubId)).Invites.Single(i => i.Email == email);

        await manager.DeleteAsync($"/clubs/{Guid.NewGuid()}/invites/{invite.Id}");

        Assert.Contains((await MembersAsync(manager, clubId)).Invites, i => i.Email == email);
    }

    [Fact]
    public async Task A_Player_can_leave_a_Club_or_take_back_a_waiting_request()
    {
        using var client = await factory.CreateSignedInClientAsync();
        using var manager = await factory.CreateManagerClientAsync();
        var member = await CreatePlayerAsync(client, "Member");
        var waiting = await CreatePlayerAsync(client, "Waiting");
        var clubId = await factory.AClubIdAsync();
        await client.PutAsync($"/clubs/{clubId}/members/{member.Id}", null);
        await client.PutAsync($"/clubs/{clubId}/members/{waiting.Id}", null);
        // Taken back while still waiting: approving a sibling would let it in (#164).
        var takeBack = await client.DeleteAsync($"/players/{waiting.Id}/clubs/{clubId}");
        await manager.PutAsync($"/clubs/{clubId}/members/{member.Id}/approval", null);

        var leave = await client.DeleteAsync($"/players/{member.Id}/clubs/{clubId}");
        var again = await client.DeleteAsync($"/players/{member.Id}/clubs/{clubId}");

        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, takeBack.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.All(await PlayersAsync(client), p => Assert.Empty(p.Clubs));
        var listed = (await MembersAsync(manager, clubId)).Members;
        Assert.DoesNotContain(listed, m => m.PlayerId == member.Id || m.PlayerId == waiting.Id);

        // Having left, coming back means asking again.
        await client.PutAsync($"/clubs/{clubId}/members/{member.Id}", null);
        Assert.False(Assert.Single((await PlayersAsync(client)).Single(p => p.Id == member.Id).Clubs).Approved);
    }

    [Fact]
    public async Task An_invited_Player_who_leaves_asks_again_to_come_back()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var email = $"returning-{Guid.NewGuid():N}@example.test";
        var clubId = await factory.AClubIdAsync();
        await manager.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email });
        using var client = await factory.CreateSignedInClientAsync(email);
        var player = await CreatePlayerAsync(client, "Pat");

        await client.DeleteAsync($"/players/{player.Id}/clubs/{clubId}");
        Assert.Empty(Assert.Single(await PlayersAsync(client)).Clubs);
        await client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null);

        // The invite was used up when it let Pat in (#164).
        Assert.False(Assert.Single(Assert.Single(await PlayersAsync(client)).Clubs).Approved);
    }

    [Fact]
    public async Task A_Player_a_member_User_adds_later_is_a_member_at_once()
    {
        using var manager = await factory.CreateManagerClientAsync();
        using var client = await factory.CreateSignedInClientAsync();
        var first = await CreatePlayerAsync(client, "First");
        var clubId = await factory.AClubIdAsync();
        await client.PutAsync($"/clubs/{clubId}/members/{first.Id}", null);
        await manager.PutAsync($"/clubs/{clubId}/members/{first.Id}/approval", null);

        var later = await CreatePlayerAsync(client, "Later");

        var club = Assert.Single((await PlayersAsync(client)).Single(p => p.Id == later.Id).Clubs);
        Assert.Equal(clubId, club.Id);
        Assert.True(club.Approved);
    }

    [Fact]
    public async Task A_Player_who_asks_is_a_member_at_once_while_a_sibling_is_a_member()
    {
        using var manager = await factory.CreateManagerClientAsync();
        using var client = await factory.CreateSignedInClientAsync();
        var member = await CreatePlayerAsync(client, "Member");
        var asking = await CreatePlayerAsync(client, "Asking");
        var clubId = await factory.AClubIdAsync();
        await client.PutAsync($"/clubs/{clubId}/members/{member.Id}", null);
        await manager.PutAsync($"/clubs/{clubId}/members/{member.Id}/approval", null);

        await client.PutAsync($"/clubs/{clubId}/members/{asking.Id}", null);

        // The app is informal: a User in a Club brings all their Players (owner, 2026-11-19).
        Assert.True(Assert.Single((await PlayersAsync(client)).Single(p => p.Id == asking.Id).Clubs).Approved);
    }

    [Fact]
    public async Task Approving_one_Player_lets_in_the_Users_other_waiting_requests()
    {
        using var manager = await factory.CreateManagerClientAsync();
        using var client = await factory.CreateSignedInClientAsync();
        var first = await CreatePlayerAsync(client, "First");
        var second = await CreatePlayerAsync(client, "Second");
        var clubId = await factory.AClubIdAsync();
        await client.PutAsync($"/clubs/{clubId}/members/{first.Id}", null);
        await client.PutAsync($"/clubs/{clubId}/members/{second.Id}", null);

        var approve = await manager.PutAsync($"/clubs/{clubId}/members/{first.Id}/approval", null);

        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);
        Assert.All(await PlayersAsync(client), p => Assert.True(Assert.Single(p.Clubs).Approved, p.Name));
    }

    /// <summary>
    /// A request left waiting from before the User was in (a race, or data from
    /// before #164) is let in when the Player asks again.
    /// </summary>
    [Fact]
    public async Task A_waiting_request_is_let_in_on_asking_again_once_the_User_is_in()
    {
        using var client = await factory.CreateSignedInClientAsync();
        var first = await CreatePlayerAsync(client, "First");
        var second = await CreatePlayerAsync(client, "Second");
        var clubId = await factory.AClubIdAsync();
        await client.PutAsync($"/clubs/{clubId}/members/{first.Id}", null);
        await client.PutAsync($"/clubs/{clubId}/members/{second.Id}", null);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
            await db.ClubMemberships
                .Where(m => m.ClubId == clubId && m.PlayerId == first.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(m => m.Status, ClubMembershipStatus.Active));
        }

        await client.PutAsync($"/clubs/{clubId}/members/{second.Id}", null);

        Assert.True(Assert.Single((await PlayersAsync(client)).Single(p => p.Id == second.Id).Clubs).Approved);
    }

    [Fact]
    public async Task A_request_still_waiting_lets_no_other_Player_of_the_User_in()
    {
        using var client = await factory.CreateSignedInClientAsync();
        var first = await CreatePlayerAsync(client, "First");
        var clubId = await factory.AClubIdAsync();
        await client.PutAsync($"/clubs/{clubId}/members/{first.Id}", null);

        var added = await CreatePlayerAsync(client, "Added");
        Assert.Empty((await PlayersAsync(client)).Single(p => p.Id == added.Id).Clubs);
        await client.PutAsync($"/clubs/{clubId}/members/{added.Id}", null);

        Assert.All(await PlayersAsync(client), p => Assert.False(Assert.Single(p.Clubs).Approved, p.Name));
    }

    [Fact]
    public async Task A_Player_a_Manager_adds_uses_the_emails_invites_up_and_joins_the_Users_Clubs()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var email = $"added-{Guid.NewGuid():N}@example.test";
        var clubId = await factory.AClubIdAsync();
        var otherClubId = await NewClubIdAsync();
        await manager.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email });

        var added = await manager.PostAsJsonAsync($"/clubs/{otherClubId}/players", new { name = "Robin", email });

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Empty(await InvitesForAsync(manager, clubId, email));
        using var client = await factory.CreateSignedInClientAsync(email);
        var robin = Assert.Single(await PlayersAsync(client));
        Assert.Equal(new[] { clubId, otherClubId }.Order(), robin.Clubs.Where(c => c.Approved).Select(c => c.Id).Order());

        // Added again, now that Robin is in both: the new Player joins both too.
        await manager.PostAsJsonAsync($"/clubs/{otherClubId}/players", new { name = "Robin Junior", email });
        var junior = (await PlayersAsync(client)).Single(p => p.Name == "Robin Junior");
        Assert.Equal(new[] { clubId, otherClubId }.Order(), junior.Clubs.Where(c => c.Approved).Select(c => c.Id).Order());
    }

    [Fact]
    public async Task A_Player_a_Manager_adds_in_the_inviting_Club_is_a_member_once_and_uses_the_invite_up()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var email = $"same-club-{Guid.NewGuid():N}@example.test";
        var clubId = await factory.AClubIdAsync();
        await manager.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email });

        var added = await manager.PostAsJsonAsync($"/clubs/{clubId}/players", new { name = "Sam", email });

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Empty(await InvitesForAsync(manager, clubId, email));
        using var client = await factory.CreateSignedInClientAsync(email);
        var club = Assert.Single(Assert.Single(await PlayersAsync(client)).Clubs);
        Assert.Equal(clubId, club.Id);
        Assert.True(club.Approved);
    }

    [Fact]
    public async Task After_one_Player_is_removed_a_new_Player_joins_while_a_sibling_is_still_a_member()
    {
        using var manager = await factory.CreateManagerClientAsync();
        using var client = await factory.CreateSignedInClientAsync();
        var stays = await CreatePlayerAsync(client, "Stays");
        var removed = await CreatePlayerAsync(client, "Removed");
        var clubId = await factory.AClubIdAsync();
        foreach (var player in new[] { stays, removed })
        {
            await client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null);
            await manager.PutAsync($"/clubs/{clubId}/members/{player.Id}/approval", null);
        }

        await manager.DeleteAsync($"/clubs/{clubId}/members/{removed.Id}");
        var later = await CreatePlayerAsync(client, "Later");

        var players = await PlayersAsync(client);
        Assert.Empty(players.Single(p => p.Id == removed.Id).Clubs);
        Assert.True(Assert.Single(players.Single(p => p.Id == later.Id).Clubs).Approved);
    }

    /// <summary>
    /// The UseUpJoinedInvites migration's SQL, run against rows made the way
    /// the code before #164 left them: it deletes an invite only where the
    /// User holds a Player in that invite's Club.
    /// </summary>
    [Fact]
    public async Task The_migration_deletes_only_invites_that_let_someone_into_their_Club()
    {
        var clubId = await factory.AClubIdAsync();
        var otherClubId = await NewClubIdAsync();
        var tag = Guid.NewGuid().ToString("N");
        string Email(string who) => $"{who}-{tag}@example.test";
        var now = DateTimeOffset.UtcNow;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
            Guid PlayerFor(string who, Guid? memberOf)
            {
                var user = new User { Id = Guid.NewGuid(), Email = Email(who), CreatedAt = now };
                var player = new Player { Id = Guid.NewGuid(), Name = who, UserId = user.Id, CreatedAt = now };
                db.Users.Add(user);
                db.Players.Add(player);
                if (memberOf is { } club)
                {
                    db.ClubMemberships.Add(new ClubMembership { ClubId = club, PlayerId = player.Id, Status = ClubMembershipStatus.Active, RequestedAt = now });
                }
                return player.Id;
            }
            PlayerFor("used", clubId);              // let in by this Club's invite
            PlayerFor("added-elsewhere", otherClubId); // a Manager added them in another Club
            PlayerFor("left", null);                // let in, then left
            foreach (var who in new[] { "used", "added-elsewhere", "left", "no-user" })
            {
                db.ClubInvites.Add(new ClubInvite { Id = Guid.NewGuid(), ClubId = clubId, Email = Email(who), CreatedAt = now });
            }
            await db.SaveChangesAsync();

            var sql = new SocialTennis.Api.Migrations.UseUpJoinedInvites().UpOperations
                .OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>()
                .Single().Sql;
            await db.Database.ExecuteSqlRawAsync(sql);
        }

        using var manager = await factory.CreateManagerClientAsync();
        var left = (await MembersAsync(manager, clubId)).Invites.Select(i => i.Email).Where(e => e.EndsWith($"-{tag}@example.test", StringComparison.Ordinal)).Order();
        Assert.Equal(new[] { Email("added-elsewhere"), Email("left"), Email("no-user") }.Order(), left);
    }

    /// <summary>
    /// Before #164 an invite outlived the Players it let in, so a Player who
    /// had left could find one still there. Asking to join uses it up.
    /// </summary>
    [Fact]
    public async Task Asking_to_join_with_an_invite_left_from_before_lets_the_Player_in_and_uses_it_up()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var email = $"left-over-{Guid.NewGuid():N}@example.test";
        using var client = await factory.CreateSignedInClientAsync(email);
        var player = await CreatePlayerAsync(client, "Returning");
        var clubId = await factory.AClubIdAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
            db.ClubInvites.Add(new ClubInvite { Id = Guid.NewGuid(), ClubId = clubId, Email = email, CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        await client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null);

        Assert.True(Assert.Single(Assert.Single(await PlayersAsync(client)).Clubs).Approved);
        Assert.Empty(await InvitesForAsync(manager, clubId, email));
    }

    [Fact]
    public async Task Another_Users_Player_cannot_be_made_to_leave()
    {
        using var owner = await factory.CreateSignedInClientAsync();
        using var other = await factory.CreateSignedInClientAsync();
        var player = await CreatePlayerAsync(owner, "Sam");
        var clubId = await factory.AClubIdAsync();
        await owner.PutAsync($"/clubs/{clubId}/members/{player.Id}", null);

        var response = await other.DeleteAsync($"/players/{player.Id}/clubs/{clubId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single(Assert.Single(await PlayersAsync(owner)).Clubs);
    }

    [Fact]
    public async Task Two_Managers_inviting_the_same_address_at_once_both_succeed()
    {
        using var first = await factory.CreateManagerClientAsync();
        using var second = await factory.CreateManagerClientAsync();
        var email = $"twice-{Guid.NewGuid():N}@example.test";
        var clubId = await factory.AClubIdAsync();

        var invites = await Task.WhenAll(
            first.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email }),
            second.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email }));

        Assert.All(invites, invite => Assert.Equal(HttpStatusCode.NoContent, invite.StatusCode));
        Assert.Single((await MembersAsync(first, clubId)).Invites, i => i.Email == email);
    }

    /// <summary>
    /// The race the per-email lock exists for: neither request may miss the
    /// other's row. Repeated, because one attempt can pass by luck.
    /// </summary>
    [Fact]
    public async Task An_invite_made_while_the_User_adds_a_Player_or_asks_to_join_always_lets_them_in()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await factory.AClubIdAsync();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var email = $"race-{attempt}-{Guid.NewGuid():N}@example.test";
            using var client = await factory.CreateSignedInClientAsync(email);
            var asking = await CreatePlayerAsync(client, "Asking");

            var results = await Task.WhenAll(
                manager.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email }),
                client.PostAsJsonAsync("/players", new { name = "New" }),
                client.PutAsync($"/clubs/{clubId}/members/{asking.Id}", null));

            Assert.All(results, result => Assert.True(result.IsSuccessStatusCode, $"{result.StatusCode}"));
            Assert.All(await PlayersAsync(client), p => Assert.True(Assert.Single(p.Clubs).Approved, p.Name));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task An_invite_needs_an_email_address(string email)
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await factory.AClubIdAsync();

        var response = await manager.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Only_a_Manager_or_Admin_can_see_approve_remove_or_invite()
    {
        using var client = await factory.CreateSignedInClientAsync();
        var player = await CreatePlayerAsync(client, "Sam");
        var clubId = await factory.AClubIdAsync();
        await client.PutAsync($"/clubs/{clubId}/members/{player.Id}", null);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/clubs/{clubId}/members")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PutAsync($"/clubs/{clubId}/members/{player.Id}/approval", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/clubs/{clubId}/members/{player.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync($"/clubs/{clubId}/invites", new { email = "friend@example.test" })).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.DeleteAsync($"/clubs/{clubId}/invites/{Guid.NewGuid()}")).StatusCode);
        Assert.False(Assert.Single(Assert.Single(await PlayersAsync(client)).Clubs).Approved);
    }

    [Fact]
    public async Task An_email_listed_in_configuration_is_an_Admin_and_can_do_what_a_Manager_can()
    {
        var admin = await factory.AdminClientAsync();
        using var player = await factory.CreateSignedInClientAsync();
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await factory.AClubIdAsync();

        var adminRoles = (await admin.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!.Roles;
        var managerRoles = (await manager.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!.Roles;
        var playerRoles = (await player.GetFromJsonAsync<GetCurrentUserResponse>("/auth/me"))!.Roles;

        Assert.Equal(["Admin", "Manager"], adminRoles.Order());
        Assert.Equal(["Manager"], managerRoles);
        Assert.Empty(playerRoles);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/clubs/{clubId}/members")).StatusCode);
    }

    [Fact]
    public async Task Joining_a_Club_that_does_not_exist_is_not_found()
    {
        using var client = await factory.CreateSignedInClientAsync();
        var player = await CreatePlayerAsync(client, "Sam");

        var response = await client.PutAsync($"/clubs/{Guid.NewGuid()}/members/{player.Id}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Players_are_refused_without_a_session()
    {
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/players")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/players", new { name = "Sam" })).StatusCode);
    }
}
