using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SocialTennis.Api.Accounts;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Jobs;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// The retention job (#117, ADR-0024): an account nobody has signed in to for
/// 24 months is deleted, as an Admin's delete does it. Here because it needs
/// Leagues, running and ended, which these tests build.
/// </summary>
/// <remarks>
/// The job is database-wide and the database is shared, so only these tests'
/// own Users are given a last sign-in that old: every other User signed in on
/// the real clock or on this factory's, which is ahead of it.
/// </remarks>
public partial class ScoreTests
{
    /// <summary>Sets when this client's User last signed in, so many months before the factory's clock.</summary>
    private async Task<Guid> LastSignedInAsync(HttpClient client, int monthsAgo)
    {
        var userId = await UserIdAsync(client);
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TennisDbContext>().Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.LastSignedInAt, (DateTimeOffset?)start.AddMonths(-monthsAgo)));
        return userId;
    }

    private async Task<bool> UserExistsAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TennisDbContext>().Users.AnyAsync(u => u.Id == userId);
    }

    private async Task<string> PlayerNameAsync(Guid playerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TennisDbContext>().Players
            .Where(p => p.Id == playerId).Select(p => p.Name).SingleAsync();
    }

    private async Task RunRetentionAsync() =>
        Assert.Equal(JobRunner.Succeeded, await JobRunner.RunAsync(factory.Services, "purge-inactive-accounts", CancellationToken.None));

    [Fact]
    public async Task An_account_with_no_sign_in_for_24_months_is_deleted_and_one_never_signed_in_to_stays()
    {
        var (manager, clubId) = await ClubAsync();
        var gone = await MemberAsync(manager, clubId, "Gwen");
        var goneUser = await LastSignedInAsync(gone.Client, PurgeInactiveAccountsJob.Months + 1);
        var recent = await MemberAsync(manager, clubId, "Ray");
        var recentUser = await LastSignedInAsync(recent.Client, PurgeInactiveAccountsJob.Months - 1);
        // Added by a Manager, never signed in (owner, 2026-11-26): kept, however long ago.
        var neverEmail = $"never-{Guid.NewGuid():N}@example.test";
        var added = await manager.PostAsJsonAsync($"/clubs/{clubId}/players", new { name = "Nell", email = neverEmail });
        added.EnsureSuccessStatusCode();
        var never = (await added.Content.ReadFromJsonAsync<AddClubPlayerResponse>())!.PlayerId;

        await RunRetentionAsync();

        Assert.False(await UserExistsAsync(goneUser));
        Assert.Equal(Player.FormerPlayerName, await PlayerNameAsync(gone.PlayerId));
        Assert.True(await UserExistsAsync(recentUser));
        Assert.Equal("Nell", await PlayerNameAsync(never));

        // Again: nothing more goes.
        await RunRetentionAsync();
        Assert.True(await UserExistsAsync(recentUser));
        Assert.Equal("Nell", await PlayerNameAsync(never));
    }

    [Fact]
    public async Task Someone_in_a_League_that_hasnt_ended_stays_and_goes_once_it_has()
    {
        // A League still running: Una and Vic on its ladder, Wes left out but in it.
        var running = await SinglesAsync(OneSetOf6);
        var una = await LastSignedInAsync(running.Una.Client, PurgeInactiveAccountsJob.Months + 6);
        var wes = await LastSignedInAsync(running.Wes.Client, PurgeInactiveAccountsJob.Months + 6);
        // A League not started yet.
        var (manager, clubId) = await ClubAsync();
        var waitingLeague = await LeagueAsync(manager, clubId, OneSetOf6);
        var pat = await ParticipantAsync(manager, clubId, waitingLeague, "Pat");
        var patUser = await LastSignedInAsync(pat.Client, PurgeInactiveAccountsJob.Months + 6);
        // A League whose one Stage has closed: it has ended.
        var endedLeague = await LeagueAsync(manager, clubId, OneSetOf6, endDate: $"{Year}-07-31");
        var ann = await ParticipantAsync(manager, clubId, endedLeague, "Ann");
        var bob = await ParticipantAsync(manager, clubId, endedLeague, "Bob");
        var league = await ManagedAsync(manager, endedLeague);
        var teams = new[] { TeamOf(league, ann.PlayerId), TeamOf(league, bob.PlayerId) };
        await StartAsync(manager, endedLeague, teams);
        var game = (await LadderOfAsync(manager, endedLeague)).Divisions.Single().Games.Single();
        (await VoidAsync(manager, game.Id)).EnsureSuccessStatusCode();
        (await CloseAsync(manager, endedLeague, [teams])).EnsureSuccessStatusCode();
        Assert.True((await LadderOfAsync(manager, endedLeague)).Ended);
        var annUser = await LastSignedInAsync(ann.Client, PurgeInactiveAccountsJob.Months + 6);

        await RunRetentionAsync();

        Assert.True(await UserExistsAsync(una));
        Assert.True(await UserExistsAsync(wes));
        Assert.True(await UserExistsAsync(patUser));
        Assert.False(await UserExistsAsync(annUser));
        Assert.Equal(Player.FormerPlayerName, await PlayerNameAsync(ann.PlayerId));
    }

    [Fact]
    public async Task Someone_who_signs_in_as_the_job_chooses_them_stays()
    {
        // The job chose them for a sign-in before its cut-off; by the time it deletes, they have signed in since.
        var (manager, clubId) = await ClubAsync();
        var back = await MemberAsync(manager, clubId, "Bea");
        var userId = await LastSignedInAsync(back.Client, 1);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();
        var auth = scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value;

        var outcome = await AccountDeletion.DeleteAsync(
            db, auth, userId, null, CancellationToken.None, lastSignedInBefore: start.AddMonths(-PurgeInactiveAccountsJob.Months));

        Assert.Equal(AccountDeletion.Outcome.SignedInSince, outcome);
        Assert.True(await UserExistsAsync(userId));
        Assert.Equal("Bea", await PlayerNameAsync(back.PlayerId));
    }
}
