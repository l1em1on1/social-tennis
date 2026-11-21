using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Features.Leagues.Contracts;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// Issue #6, part 2: a Player asks to join a doubles League with a partner,
/// approval puts both in as a Team, either of them can take the request back,
/// and a Player is in at most one waiting request per League.
/// </summary>
public class JoinAsTeamTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
{
    private static object ALeague(string type) => new
    {
        name = $"{type} League",
        type,
        structure = "Divisional",
        startDate = "2026-12-01",
        endDate = "2027-03-31",
        stageLength = new { count = 1, unit = "Months" },
        stageRoll = "Manual",
        scoreFormat = new { gamesPerSet = 6, setsPerGame = 1, matchTiebreakDecider = false },
    };

    private async Task<Guid> NewClubAsync()
    {
        var admin = await factory.AdminClientAsync();
        var created = await admin.PostAsJsonAsync("/clubs", new { name = $"Pairs Club {Guid.NewGuid():N}" });
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<CreateClubResponse>())!.Id;
    }

    private static async Task<Guid> CreateLeagueAsync(HttpClient manager, Guid clubId, string type = "DoublesMix")
    {
        var response = await manager.PostAsJsonAsync($"/clubs/{clubId}/leagues", ALeague(type));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateLeagueResponse>())!.Id;
    }

    /// <summary>A Player of <paramref name="client"/>'s User, approved into the Club.</summary>
    private static async Task<Guid> PlayerInClubAsync(HttpClient client, HttpClient manager, Guid clubId, string name, string? gender = null)
    {
        var created = await client.PostAsJsonAsync("/players", new { name, gender });
        created.EnsureSuccessStatusCode();
        var playerId = (await created.Content.ReadFromJsonAsync<CreatePlayerResponse>())!.Id;
        (await client.PutAsync($"/clubs/{clubId}/members/{playerId}", null)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/clubs/{clubId}/members/{playerId}/approval", null)).EnsureSuccessStatusCode();
        return playerId;
    }

    /// <summary>A fresh User with one Player, approved into the Club.</summary>
    private async Task<(HttpClient Client, Guid PlayerId)> MemberAsync(HttpClient manager, Guid clubId, string name, string? gender = null)
    {
        var client = await factory.CreateSignedInClientAsync();
        return (client, await PlayerInClubAsync(client, manager, clubId, name, gender));
    }

    private static Task<HttpResponseMessage> AskAsync(HttpClient client, Guid leagueId, Guid playerId, Guid? partnerId = null) =>
        client.PostAsJsonAsync($"/leagues/{leagueId}/join-requests", new { playerId, partnerId });

    private static Task<HttpResponseMessage> TakeBackAsync(HttpClient client, Guid leagueId, Guid askerId) =>
        client.DeleteAsync($"/leagues/{leagueId}/join-requests/{askerId}");

    private static Task<HttpResponseMessage> ApproveAsync(HttpClient manager, Guid leagueId, Guid askerId) =>
        manager.PutAsync($"/leagues/{leagueId}/join-requests/{askerId}/approval", null);

    private static async Task<GetLeagueResponse> LeagueAsync(HttpClient manager, Guid leagueId) =>
        (await manager.GetFromJsonAsync<GetLeagueResponse>($"/leagues/{leagueId}"))!;

    private static async Task<PlayerLeague> SeenByAsync(HttpClient client, Guid playerId, Guid leagueId) =>
        (await client.GetFromJsonAsync<GetPlayerLeaguesResponse>($"/players/{playerId}/leagues"))!
            .Leagues.Single(l => l.Id == leagueId);

    private static async Task<IReadOnlyList<LeaguePartner>> PartnersAsync(HttpClient client, Guid leagueId, Guid playerId) =>
        (await client.GetFromJsonAsync<GetLeaguePartnersResponse>($"/leagues/{leagueId}/partners?playerId={playerId}"))!.Partners;

    private static async Task<string?> ConflictTypeAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Type;
    }

    [Fact]
    public async Task A_pair_asks_together_and_approval_puts_both_in_as_a_Team()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (alex, alexId) = await MemberAsync(manager, clubId, "Alex", "Female");
        var (jo, joId) = await MemberAsync(manager, clubId, "Jo", "Male");

        Assert.Equal(HttpStatusCode.NoContent, (await AskAsync(alex, leagueId, alexId, joId)).StatusCode);

        // The Manager sees one request, naming the partner; each Player sees their side of it.
        var request = Assert.Single((await LeagueAsync(manager, leagueId)).JoinRequests);
        Assert.Equal((alexId, PlayerGender.Female), (request.PlayerId, request.Gender));
        Assert.Equal((joId, "Jo", PlayerGender.Male), (request.Partner!.PlayerId, request.Partner.Name, request.Partner.Gender));
        var askerView = await SeenByAsync(alex, alexId, leagueId);
        Assert.Equal((LeagueJoinRequestStatus.Pending, "Jo"), (askerView.JoinRequest!.Status, askerView.JoinRequest.PartnerName));
        var partnerView = await SeenByAsync(jo, joId, leagueId);
        Assert.Null(partnerView.JoinRequest);
        Assert.Equal((alexId, "Alex"), (partnerView.PartnerRequest!.PlayerId, partnerView.PartnerRequest.Name));

        Assert.Equal(HttpStatusCode.NoContent, (await ApproveAsync(manager, leagueId, alexId)).StatusCode);

        var league = await LeagueAsync(manager, leagueId);
        var team = Assert.Single(league.Teams);
        Assert.Equal(new[] { alexId, joId }.Order(), team.PlayerIds.Order());
        Assert.All(league.Participants, p => Assert.Equal((team.Id, request.RequestedAt), (p.TeamId, p.JoinedAt)));
        Assert.Equal(2, league.Participants.Count);
        Assert.Equal("Jo", (await SeenByAsync(alex, alexId, leagueId)).PartnerName);
        partnerView = await SeenByAsync(jo, joId, leagueId);
        Assert.Equal((true, "Alex", (PlayerLeaguePartnerRequest?)null), (partnerView.Joined, partnerView.PartnerName, partnerView.PartnerRequest));
    }

    [Fact]
    public async Task Two_Players_of_one_User_can_ask_as_a_pair()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (parent, parentId) = await MemberAsync(manager, clubId, "Parent");
        var childId = await PlayerInClubAsync(parent, manager, clubId, "Child");

        Assert.Contains(await PartnersAsync(parent, leagueId, parentId), p => p.PlayerId == childId);
        Assert.Equal(HttpStatusCode.NoContent, (await AskAsync(parent, leagueId, parentId, childId)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ApproveAsync(manager, leagueId, parentId)).StatusCode);

        Assert.Equal(new[] { parentId, childId }.Order(), Assert.Single((await LeagueAsync(manager, leagueId)).Teams).PlayerIds.Order());
    }

    [Fact]
    public async Task A_Manager_adding_the_Player_who_asked_brings_their_partner_in_with_them()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (alex, alexId) = await MemberAsync(manager, clubId, "Alex");
        var (_, joId) = await MemberAsync(manager, clubId, "Jo");
        (await AskAsync(alex, leagueId, alexId, joId)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent, (await manager.PutAsync($"/leagues/{leagueId}/participants/{alexId}", null)).StatusCode);

        var league = await LeagueAsync(manager, leagueId);
        Assert.Equal(new[] { alexId, joId }.Order(), Assert.Single(league.Teams).PlayerIds.Order());
        Assert.Equal(LeagueJoinRequestStatus.Approved, Assert.Single(league.JoinRequests).Status);
    }

    [Fact]
    public async Task A_partner_already_in_without_a_partner_is_paired_and_keeps_their_join_time()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (alex, alexId) = await MemberAsync(manager, clubId, "Alex");
        var (_, joId) = await MemberAsync(manager, clubId, "Jo");
        (await manager.PutAsync($"/leagues/{leagueId}/participants/{joId}", null)).EnsureSuccessStatusCode();
        var joJoined = Assert.Single((await LeagueAsync(manager, leagueId)).Participants).JoinedAt;

        Assert.Contains(await PartnersAsync(alex, leagueId, alexId), p => p.PlayerId == joId);
        (await AskAsync(alex, leagueId, alexId, joId)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await ApproveAsync(manager, leagueId, alexId)).StatusCode);

        var league = await LeagueAsync(manager, leagueId);
        var team = Assert.Single(league.Teams);
        Assert.Equal(new[] { alexId, joId }.Order(), team.PlayerIds.Order());
        Assert.Equal(joJoined, league.Participants.Single(p => p.PlayerId == joId).JoinedAt);
    }

    [Fact]
    public async Task Approval_is_refused_when_the_partner_has_been_paired_with_someone_else_meanwhile()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (alex, alexId) = await MemberAsync(manager, clubId, "Alex");
        var (_, joId) = await MemberAsync(manager, clubId, "Jo");
        var (_, samId) = await MemberAsync(manager, clubId, "Sam");
        (await AskAsync(alex, leagueId, alexId, joId)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/leagues/{leagueId}/participants/{joId}", null)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/leagues/{leagueId}/participants/{samId}", null)).EnsureSuccessStatusCode();
        (await manager.PostAsJsonAsync($"/leagues/{leagueId}/teams", new { firstPlayerId = joId, secondPlayerId = samId }))
            .EnsureSuccessStatusCode();

        Assert.Equal("already-in-a-team", await ConflictTypeAsync(await ApproveAsync(manager, leagueId, alexId)));
        Assert.Equal("already-in-a-team", await ConflictTypeAsync(await manager.PutAsync($"/leagues/{leagueId}/participants/{alexId}", null)));

        // Nothing changed: Alex isn't in, and the request still waits for the Manager to reject it.
        var league = await LeagueAsync(manager, leagueId);
        Assert.DoesNotContain(league.Participants, p => p.PlayerId == alexId);
        Assert.Single(league.Teams);
        Assert.Equal(LeagueJoinRequestStatus.Pending, Assert.Single(league.JoinRequests).Status);
    }

    [Fact]
    public async Task Approval_is_refused_when_the_partner_has_left_the_Club()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (alex, alexId) = await MemberAsync(manager, clubId, "Alex");
        var (jo, joId) = await MemberAsync(manager, clubId, "Jo");
        (await AskAsync(alex, leagueId, alexId, joId)).EnsureSuccessStatusCode();
        (await jo.DeleteAsync($"/players/{joId}/clubs/{clubId}")).EnsureSuccessStatusCode();

        Assert.Equal("partner-not-a-club-member", await ConflictTypeAsync(await ApproveAsync(manager, leagueId, alexId)));
        Assert.Empty((await LeagueAsync(manager, leagueId)).Participants);
    }

    [Fact]
    public async Task A_rejection_stops_only_the_Player_who_asked()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (alex, alexId) = await MemberAsync(manager, clubId, "Alex");
        var (jo, joId) = await MemberAsync(manager, clubId, "Jo");
        (await AskAsync(alex, leagueId, alexId, joId)).EnsureSuccessStatusCode();

        (await manager.PutAsync($"/leagues/{leagueId}/join-requests/{alexId}/rejection", null)).EnsureSuccessStatusCode();

        Assert.Equal("already-asked", await ConflictTypeAsync(await AskAsync(alex, leagueId, alexId)));
        var partnerView = await SeenByAsync(jo, joId, leagueId);
        Assert.Equal(((PlayerLeagueJoinRequest?)null, (PlayerLeaguePartnerRequest?)null), (partnerView.JoinRequest, partnerView.PartnerRequest));
        Assert.Equal(HttpStatusCode.NoContent, (await AskAsync(jo, leagueId, joId)).StatusCode);
    }

    [Fact]
    public async Task The_partner_can_take_the_request_back_and_the_Player_who_asked_may_ask_again()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (alex, alexId) = await MemberAsync(manager, clubId, "Alex");
        var (jo, joId) = await MemberAsync(manager, clubId, "Jo");
        (await AskAsync(alex, leagueId, alexId, joId)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent, (await TakeBackAsync(jo, leagueId, alexId)).StatusCode);

        Assert.Empty((await LeagueAsync(manager, leagueId)).JoinRequests);
        Assert.Null((await SeenByAsync(alex, alexId, leagueId)).JoinRequest);
        Assert.Null((await SeenByAsync(jo, joId, leagueId)).PartnerRequest);
        Assert.Equal(HttpStatusCode.NoContent, (await AskAsync(alex, leagueId, alexId, joId)).StatusCode);
    }

    [Fact]
    public async Task The_Player_who_asked_can_take_the_request_back_alone_or_with_a_partner()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (alex, alexId) = await MemberAsync(manager, clubId, "Alex");
        var (_, joId) = await MemberAsync(manager, clubId, "Jo");

        (await AskAsync(alex, leagueId, alexId)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await TakeBackAsync(alex, leagueId, alexId)).StatusCode);
        (await AskAsync(alex, leagueId, alexId, joId)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await TakeBackAsync(alex, leagueId, alexId)).StatusCode);

        Assert.Empty((await LeagueAsync(manager, leagueId)).JoinRequests);
    }

    [Fact]
    public async Task Only_the_pair_can_take_a_request_back_and_only_while_it_waits()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (alex, alexId) = await MemberAsync(manager, clubId, "Alex");
        var (jo, joId) = await MemberAsync(manager, clubId, "Jo");
        var (sam, _) = await MemberAsync(manager, clubId, "Sam");
        (await AskAsync(alex, leagueId, alexId, joId)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await TakeBackAsync(sam, leagueId, alexId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await TakeBackAsync(jo, leagueId, joId)).StatusCode);

        (await ApproveAsync(manager, leagueId, alexId)).EnsureSuccessStatusCode();
        Assert.Equal("join-request-decided", await ConflictTypeAsync(await TakeBackAsync(jo, leagueId, alexId)));
        Assert.Equal(2, (await LeagueAsync(manager, leagueId)).Participants.Count);
    }

    [Fact]
    public async Task A_Player_is_in_at_most_one_waiting_request()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (alex, alexId) = await MemberAsync(manager, clubId, "Alex");
        var (jo, joId) = await MemberAsync(manager, clubId, "Jo");
        var (sam, samId) = await MemberAsync(manager, clubId, "Sam");
        var (chris, chrisId) = await MemberAsync(manager, clubId, "Chris");
        (await AskAsync(alex, leagueId, alexId, joId)).EnsureSuccessStatusCode();

        Assert.Equal("partner-in-a-request", await ConflictTypeAsync(await AskAsync(sam, leagueId, samId, joId)));
        Assert.Equal("partner-in-a-request", await ConflictTypeAsync(await AskAsync(sam, leagueId, samId, alexId)));
        Assert.Equal("named-as-partner", await ConflictTypeAsync(await AskAsync(jo, leagueId, joId)));
        Assert.Equal("named-as-partner", await ConflictTypeAsync(await AskAsync(jo, leagueId, joId, samId)));

        // Asking alone counts too.
        (await AskAsync(sam, leagueId, samId)).EnsureSuccessStatusCode();
        Assert.Equal("partner-in-a-request", await ConflictTypeAsync(await AskAsync(chris, leagueId, chrisId, samId)));

        Assert.Equal(2, (await LeagueAsync(manager, leagueId)).JoinRequests.Count);
    }

    [Fact]
    public async Task Naming_a_partner_is_refused_who_is_paired_outside_the_Club_or_oneself()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (alex, alexId) = await MemberAsync(manager, clubId, "Alex");
        var (_, joId) = await MemberAsync(manager, clubId, "Jo");
        var (_, samId) = await MemberAsync(manager, clubId, "Sam");
        var (_, outsiderId) = await MemberAsync(manager, await NewClubAsync(), "Outsider");
        (await manager.PutAsync($"/leagues/{leagueId}/participants/{joId}", null)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/leagues/{leagueId}/participants/{samId}", null)).EnsureSuccessStatusCode();
        (await manager.PostAsJsonAsync($"/leagues/{leagueId}/teams", new { firstPlayerId = joId, secondPlayerId = samId }))
            .EnsureSuccessStatusCode();

        Assert.Equal("partner-in-a-team", await ConflictTypeAsync(await AskAsync(alex, leagueId, alexId, joId)));
        Assert.Equal(HttpStatusCode.NotFound, (await AskAsync(alex, leagueId, alexId, outsiderId)).StatusCode);
        var self = await AskAsync(alex, leagueId, alexId, alexId);
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
        Assert.True((await self.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.ContainsKey("partnerId"));
        Assert.Empty((await LeagueAsync(manager, leagueId)).JoinRequests);
    }

    [Theory]
    [InlineData("SinglesMixed")]
    [InlineData("Foursomes")]
    public async Task Only_a_doubles_League_takes_a_partner(string type)
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, type);
        var (alex, alexId) = await MemberAsync(manager, clubId, "Alex");
        var (_, joId) = await MemberAsync(manager, clubId, "Jo");

        Assert.Equal("not-a-doubles-league", await ConflictTypeAsync(await AskAsync(alex, leagueId, alexId, joId)));
        Assert.Equal("not-a-doubles-league", await ConflictTypeAsync(await alex.GetAsync($"/leagues/{leagueId}/partners?playerId={alexId}")));
    }

    [Fact]
    public async Task The_partners_are_the_Club_members_free_to_pair_by_name_only()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (alex, alexId) = await MemberAsync(manager, clubId, "Alex");
        var (_, unpairedId) = await MemberAsync(manager, clubId, "Unpaired");
        var (_, pairedId) = await MemberAsync(manager, clubId, "Paired");
        var (_, otherPairedId) = await MemberAsync(manager, clubId, "Other paired");
        var (asking, askingId) = await MemberAsync(manager, clubId, "Asking");
        var (_, namedId) = await MemberAsync(manager, clubId, "Named");
        var (_, freeId) = await MemberAsync(manager, clubId, "Free");
        await MemberAsync(manager, clubId, "Member");
        await MemberAsync(manager, await NewClubAsync(), "Elsewhere");
        foreach (var id in new[] { unpairedId, pairedId, otherPairedId })
        {
            (await manager.PutAsync($"/leagues/{leagueId}/participants/{id}", null)).EnsureSuccessStatusCode();
        }
        (await manager.PostAsJsonAsync($"/leagues/{leagueId}/teams", new { firstPlayerId = pairedId, secondPlayerId = otherPairedId }))
            .EnsureSuccessStatusCode();
        (await AskAsync(asking, leagueId, askingId, namedId)).EnsureSuccessStatusCode();
        // A membership still waiting for the Manager doesn't count. (A new User's:
        // another Player of a User already in the Club is let in at once.)
        var newcomer = await factory.CreateSignedInClientAsync();
        var pendingMember = (await (await newcomer.PostAsJsonAsync("/players", new { name = "Not yet a member" }))
            .Content.ReadFromJsonAsync<CreatePlayerResponse>())!.Id;
        (await newcomer.PutAsync($"/clubs/{clubId}/members/{pendingMember}", null)).EnsureSuccessStatusCode();

        var partners = await PartnersAsync(alex, leagueId, alexId);

        Assert.Equal(["Free", "Member", "Unpaired"], partners.Select(p => p.Name));
        Assert.Equal(freeId, partners[0].PlayerId);
        var json = await alex.GetStringAsync($"/leagues/{leagueId}/partners?playerId={alexId}");
        Assert.DoesNotContain("@", json);
    }

    [Fact]
    public async Task Another_Users_Player_has_no_partner_list()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (_, alexId) = await MemberAsync(manager, clubId, "Alex");
        var (jo, _) = await MemberAsync(manager, clubId, "Jo");

        Assert.Equal(HttpStatusCode.NotFound, (await jo.GetAsync($"/leagues/{leagueId}/partners?playerId={alexId}")).StatusCode);
    }

    [Fact]
    public async Task Removing_the_Player_who_asked_leaves_their_partner_unpaired_and_free_to_ask()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId);
        var (alex, alexId) = await MemberAsync(manager, clubId, "Alex");
        var (_, joId) = await MemberAsync(manager, clubId, "Jo");
        (await AskAsync(alex, leagueId, alexId, joId)).EnsureSuccessStatusCode();
        (await ApproveAsync(manager, leagueId, alexId)).EnsureSuccessStatusCode();

        (await manager.DeleteAsync($"/leagues/{leagueId}/participants/{alexId}")).EnsureSuccessStatusCode();

        var league = await LeagueAsync(manager, leagueId);
        Assert.Null(Assert.Single(league.Participants).TeamId);
        Assert.Empty(league.Teams);
        // Alex's approved request went with them, so they may ask again, with Jo or anyone free.
        Assert.Equal(HttpStatusCode.NoContent, (await AskAsync(alex, leagueId, alexId, joId)).StatusCode);
    }
}
