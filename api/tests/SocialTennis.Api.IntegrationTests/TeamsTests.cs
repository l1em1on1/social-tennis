using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Features.Leagues.Contracts;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// Issue #6, part 1: every singles participant is a Team of one, a Manager
/// pairs a doubles League's Players into Teams and can break them up, and a
/// Foursomes League has no Teams.
/// </summary>
public class TeamsTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
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
        var created = await admin.PostAsJsonAsync("/clubs", new { name = $"Teams Club {Guid.NewGuid():N}" });
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<CreateClubResponse>())!.Id;
    }

    private static async Task<Guid> CreateLeagueAsync(HttpClient manager, Guid clubId, string type)
    {
        var response = await manager.PostAsJsonAsync($"/clubs/{clubId}/leagues", ALeague(type));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateLeagueResponse>())!.Id;
    }

    /// <summary>A fresh User with one Player, approved into the Club.</summary>
    private async Task<(HttpClient Client, Guid PlayerId)> MemberAsync(HttpClient manager, Guid clubId, string name)
    {
        var client = await factory.CreateSignedInClientAsync();
        var created = await client.PostAsJsonAsync("/players", new { name });
        var playerId = (await created.Content.ReadFromJsonAsync<CreatePlayerResponse>())!.Id;
        (await client.PutAsync($"/clubs/{clubId}/members/{playerId}", null)).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/clubs/{clubId}/members/{playerId}/approval", null)).EnsureSuccessStatusCode();
        return (client, playerId);
    }

    /// <summary>A Club member the Manager puts straight into the League.</summary>
    private async Task<(HttpClient Client, Guid PlayerId)> ParticipantAsync(HttpClient manager, Guid clubId, Guid leagueId, string name)
    {
        var member = await MemberAsync(manager, clubId, name);
        (await manager.PutAsync($"/leagues/{leagueId}/participants/{member.PlayerId}", null)).EnsureSuccessStatusCode();
        return member;
    }

    private static async Task<GetLeagueResponse> LeagueAsync(HttpClient manager, Guid leagueId) =>
        (await manager.GetFromJsonAsync<GetLeagueResponse>($"/leagues/{leagueId}"))!;

    private static async Task<string?> PartnerNameAsync(HttpClient client, Guid playerId, Guid leagueId) =>
        (await client.GetFromJsonAsync<GetPlayerLeaguesResponse>($"/players/{playerId}/leagues"))!
            .Leagues.Single(l => l.Id == leagueId).PartnerName;

    private static Task<HttpResponseMessage> PairAsync(HttpClient manager, Guid leagueId, Guid first, Guid second) =>
        manager.PostAsJsonAsync($"/leagues/{leagueId}/teams", new { firstPlayerId = first, secondPlayerId = second });

    private static async Task<string?> ConflictTypeAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Type;
    }

    [Fact]
    public async Task A_singles_participant_is_a_Team_of_one_whether_approved_or_added()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, "SinglesMixed");
        var (asker, askerId) = await MemberAsync(manager, clubId, "Asker");
        (await asker.PostAsJsonAsync($"/leagues/{leagueId}/join-requests", new { playerId = askerId })).EnsureSuccessStatusCode();
        (await manager.PutAsync($"/leagues/{leagueId}/join-requests/{askerId}/approval", null)).EnsureSuccessStatusCode();
        var (_, addedId) = await ParticipantAsync(manager, clubId, leagueId, "Added");

        var league = await LeagueAsync(manager, leagueId);

        Assert.Equal(2, league.Teams.Count);
        Assert.All(league.Participants, p => Assert.Equal([p.PlayerId], league.Teams.Single(t => t.Id == p.TeamId).PlayerIds));
        Assert.Equal(new[] { askerId, addedId }.Order(), league.Teams.Select(t => Assert.Single(t.PlayerIds)).Order());
    }

    [Fact]
    public async Task A_doubles_participant_starts_without_a_partner()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, "DoublesMix");
        await ParticipantAsync(manager, clubId, leagueId, "Alex");

        var league = await LeagueAsync(manager, leagueId);

        Assert.Null(Assert.Single(league.Participants).TeamId);
        Assert.Empty(league.Teams);
    }

    [Fact]
    public async Task A_Foursomes_League_has_no_Teams_and_its_Players_are_not_paired()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, "Foursomes");
        var (_, first) = await ParticipantAsync(manager, clubId, leagueId, "First");
        var (_, second) = await ParticipantAsync(manager, clubId, leagueId, "Second");

        var pair = await PairAsync(manager, leagueId, first, second);

        Assert.Equal("not-a-doubles-league", await ConflictTypeAsync(pair));
        var league = await LeagueAsync(manager, leagueId);
        Assert.Empty(league.Teams);
        Assert.All(league.Participants, p => Assert.Null(p.TeamId));
    }

    [Fact]
    public async Task The_Manager_pairs_two_Players_and_each_sees_the_other_as_partner()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, "DoublesMix");
        var (alex, alexId) = await ParticipantAsync(manager, clubId, leagueId, "Alex");
        var (jo, joId) = await ParticipantAsync(manager, clubId, leagueId, "Jo");

        var pair = await PairAsync(manager, leagueId, alexId, joId);

        Assert.Equal(HttpStatusCode.Created, pair.StatusCode);
        var teamId = (await pair.Content.ReadFromJsonAsync<PairTeamResponse>())!.Id;
        var league = await LeagueAsync(manager, leagueId);
        var team = Assert.Single(league.Teams);
        Assert.Equal(teamId, team.Id);
        Assert.Equal(new[] { alexId, joId }.Order(), team.PlayerIds.Order());
        Assert.All(league.Participants, p => Assert.Equal(teamId, p.TeamId));
        Assert.Equal("Jo", await PartnerNameAsync(alex, alexId, leagueId));
        Assert.Equal("Alex", await PartnerNameAsync(jo, joId, leagueId));
    }

    [Fact]
    public async Task Two_Players_of_one_login_can_be_paired()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, "DoublesMix");
        var (parent, parentId) = await ParticipantAsync(manager, clubId, leagueId, "Parent");
        var child = await parent.PostAsJsonAsync("/players", new { name = "Child" });
        var childId = (await child.Content.ReadFromJsonAsync<CreatePlayerResponse>())!.Id;
        (await manager.PutAsync($"/leagues/{leagueId}/participants/{childId}", null)).EnsureSuccessStatusCode();

        var pair = await PairAsync(manager, leagueId, parentId, childId);

        Assert.Equal(HttpStatusCode.Created, pair.StatusCode);
        Assert.Equal("Child", await PartnerNameAsync(parent, parentId, leagueId));
    }

    [Fact]
    public async Task A_Teams_Rating_is_the_average_of_its_Players_Ratings_on_read()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, "DoublesMen");
        var (_, first) = await ParticipantAsync(manager, clubId, leagueId, "First");
        var (_, second) = await ParticipantAsync(manager, clubId, leagueId, "Second");
        (await manager.PutAsJsonAsync($"/players/{first}/rating", new { rating = 4 })).EnsureSuccessStatusCode();
        (await PairAsync(manager, leagueId, first, second)).EnsureSuccessStatusCode();

        Assert.Equal(2.5, Assert.Single((await LeagueAsync(manager, leagueId)).Teams).Rating);

        (await manager.PutAsJsonAsync($"/players/{second}/rating", new { rating = 8 })).EnsureSuccessStatusCode();
        Assert.Equal(6, Assert.Single((await LeagueAsync(manager, leagueId)).Teams).Rating);
    }

    [Fact]
    public async Task A_Player_with_a_partner_or_outside_the_League_is_not_paired()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, "DoublesWomen");
        var (_, first) = await ParticipantAsync(manager, clubId, leagueId, "First");
        var (_, second) = await ParticipantAsync(manager, clubId, leagueId, "Second");
        var (_, third) = await ParticipantAsync(manager, clubId, leagueId, "Third");
        var (_, outsider) = await MemberAsync(manager, clubId, "Outsider");
        (await PairAsync(manager, leagueId, first, second)).EnsureSuccessStatusCode();

        Assert.Equal("not-unpaired", await ConflictTypeAsync(await PairAsync(manager, leagueId, first, third)));
        Assert.Equal("not-unpaired", await ConflictTypeAsync(await PairAsync(manager, leagueId, third, outsider)));
        Assert.Single((await LeagueAsync(manager, leagueId)).Teams);
    }

    [Fact]
    public async Task A_Player_is_not_paired_with_themselves()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, "DoublesMix");
        var (_, playerId) = await ParticipantAsync(manager, clubId, leagueId, "Alone");

        var pair = await PairAsync(manager, leagueId, playerId, playerId);

        Assert.Equal(HttpStatusCode.BadRequest, pair.StatusCode);
        Assert.Empty((await LeagueAsync(manager, leagueId)).Teams);
    }

    [Fact]
    public async Task A_singles_Leagues_Teams_are_not_paired_or_broken_up_by_the_Manager()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, "SinglesMen");
        var (_, first) = await ParticipantAsync(manager, clubId, leagueId, "First");
        var (_, second) = await ParticipantAsync(manager, clubId, leagueId, "Second");
        var teamId = (await LeagueAsync(manager, leagueId)).Teams[0].Id;

        Assert.Equal("not-a-doubles-league", await ConflictTypeAsync(await PairAsync(manager, leagueId, first, second)));
        Assert.Equal("not-a-doubles-league", await ConflictTypeAsync(await manager.DeleteAsync($"/leagues/{leagueId}/teams/{teamId}")));
        Assert.Equal(2, (await LeagueAsync(manager, leagueId)).Teams.Count);
    }

    [Fact]
    public async Task Pairing_or_unpairing_in_a_League_that_isnt_there_is_a_404()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var missing = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await PairAsync(manager, missing, Guid.NewGuid(), Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.DeleteAsync($"/leagues/{missing}/teams/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Unpairing_leaves_both_Players_in_the_League_ready_to_be_paired_again()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, "DoublesMix");
        var (alex, alexId) = await ParticipantAsync(manager, clubId, leagueId, "Alex");
        var (_, joId) = await ParticipantAsync(manager, clubId, leagueId, "Jo");
        var (_, samId) = await ParticipantAsync(manager, clubId, leagueId, "Sam");
        var pair = await PairAsync(manager, leagueId, alexId, joId);
        var teamId = (await pair.Content.ReadFromJsonAsync<PairTeamResponse>())!.Id;

        var unpair = await manager.DeleteAsync($"/leagues/{leagueId}/teams/{teamId}");

        Assert.Equal(HttpStatusCode.NoContent, unpair.StatusCode);
        var league = await LeagueAsync(manager, leagueId);
        Assert.Empty(league.Teams);
        Assert.Equal(3, league.Participants.Count);
        Assert.All(league.Participants, p => Assert.Null(p.TeamId));
        Assert.Null(await PartnerNameAsync(alex, alexId, leagueId));
        Assert.Equal(HttpStatusCode.Created, (await PairAsync(manager, leagueId, alexId, samId)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync($"/leagues/{leagueId}/teams/{teamId}")).StatusCode);
    }

    [Fact]
    public async Task Removing_a_paired_Player_leaves_their_partner_unpaired()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, "DoublesMix");
        var (_, alexId) = await ParticipantAsync(manager, clubId, leagueId, "Alex");
        var (jo, joId) = await ParticipantAsync(manager, clubId, leagueId, "Jo");
        (await PairAsync(manager, leagueId, alexId, joId)).EnsureSuccessStatusCode();

        (await manager.DeleteAsync($"/leagues/{leagueId}/participants/{alexId}")).EnsureSuccessStatusCode();

        var league = await LeagueAsync(manager, leagueId);
        Assert.Empty(league.Teams);
        Assert.Null(Assert.Single(league.Participants).TeamId);
        Assert.Null(await PartnerNameAsync(jo, joId, leagueId));
    }

    [Fact]
    public async Task Removing_a_singles_Player_takes_their_Team_of_one_too()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, "SinglesWomen");
        var (_, leavingId) = await ParticipantAsync(manager, clubId, leagueId, "Leaving");
        var (_, stayingId) = await ParticipantAsync(manager, clubId, leagueId, "Staying");

        (await manager.DeleteAsync($"/leagues/{leagueId}/participants/{leavingId}")).EnsureSuccessStatusCode();

        Assert.Equal([stayingId], Assert.Single((await LeagueAsync(manager, leagueId)).Teams).PlayerIds);
    }

    [Fact]
    public async Task Two_Managers_pairing_the_same_Player_at_once_make_one_Team()
    {
        using var manager = await factory.CreateManagerClientAsync();
        using var other = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var leagueId = await CreateLeagueAsync(manager, clubId, "DoublesMix");
        var (_, shared) = await ParticipantAsync(manager, clubId, leagueId, "Shared");
        var (_, first) = await ParticipantAsync(manager, clubId, leagueId, "First");
        var (_, second) = await ParticipantAsync(manager, clubId, leagueId, "Second");

        var results = await Task.WhenAll(PairAsync(manager, leagueId, shared, first), PairAsync(other, leagueId, shared, second));

        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }.Order(), results.Select(r => r.StatusCode).Order());
        Assert.Single((await LeagueAsync(manager, leagueId)).Teams);
    }

    [Fact]
    public async Task A_participant_cant_be_put_in_another_Leagues_Team()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var singlesId = await CreateLeagueAsync(manager, clubId, "SinglesMixed");
        var doublesId = await CreateLeagueAsync(manager, clubId, "DoublesMix");
        await ParticipantAsync(manager, clubId, singlesId, "Singles");
        var (_, playerId) = await ParticipantAsync(manager, clubId, doublesId, "Doubles");
        var otherTeamId = (await LeagueAsync(manager, singlesId)).Teams[0].Id;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
        var update = db.LeagueParticipants
            .Where(p => p.LeagueId == doublesId && p.PlayerId == playerId)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.TeamId, otherTeamId));

        var refused = await Assert.ThrowsAsync<PostgresException>(() => update);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, refused.SqlState);
    }

    /// <summary>
    /// The Teams migration's SQL, run against participants made the way the
    /// code before #6 left them, with no Team: a singles participant gets a
    /// Team of one; doubles and Foursomes participants get none.
    /// </summary>
    [Fact]
    public async Task The_migration_gives_each_singles_participant_a_Team_of_one()
    {
        using var manager = await factory.CreateManagerClientAsync();
        var clubId = await NewClubAsync();
        var singlesId = await CreateLeagueAsync(manager, clubId, "SinglesMen");
        var doublesId = await CreateLeagueAsync(manager, clubId, "DoublesMen");
        var foursomesId = await CreateLeagueAsync(manager, clubId, "Foursomes");
        var (_, first) = await MemberAsync(manager, clubId, "First");
        var (_, second) = await MemberAsync(manager, clubId, "Second");
        var leagues = new[] { singlesId, doublesId, foursomesId };

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
            foreach (var leagueId in leagues)
            {
                foreach (var playerId in new[] { first, second })
                {
                    db.LeagueParticipants.Add(new LeagueParticipant { LeagueId = leagueId, PlayerId = playerId, JoinedAt = DateTimeOffset.UtcNow });
                }
            }
            await db.SaveChangesAsync();

            var sql = new SocialTennis.Api.Migrations.Teams().UpOperations.OfType<SqlOperation>().Single().Sql;
            await db.Database.ExecuteSqlRawAsync(sql);
        }

        var singles = await LeagueAsync(manager, singlesId);
        Assert.Equal(2, singles.Teams.Count);
        Assert.All(singles.Participants, p => Assert.Equal([p.PlayerId], singles.Teams.Single(t => t.Id == p.TeamId).PlayerIds));
        foreach (var leagueId in new[] { doublesId, foursomesId })
        {
            var league = await LeagueAsync(manager, leagueId);
            Assert.Empty(league.Teams);
            Assert.All(league.Participants, p => Assert.Null(p.TeamId));
        }
    }
}
