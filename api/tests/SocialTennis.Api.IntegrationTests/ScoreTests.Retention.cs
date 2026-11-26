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
/// a League with results, which these tests build.
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
    public async Task Playing_in_a_League_doesnt_keep_an_account_with_no_sign_in_for_24_months()
    {
        // Una plays in a running League, but hasn't signed in for 25 months: she goes, and her results stay, as Former player's.
        var running = await SinglesAsync(OneSetOf6);
        (await SubmitAsync(running.Una.Client, running.GameId, running.Una.PlayerId, [Set(6, 2)])).EnsureSuccessStatusCode();
        var una = await LastSignedInAsync(running.Una.Client, PurgeInactiveAccountsJob.Months + 1);

        await RunRetentionAsync();

        Assert.False(await UserExistsAsync(una));
        var game = await GameAsync(running.Vic.Client, running.GameId);
        Assert.Equal(Player.FormerPlayerName, game.Sides.Single(s => s.Side == 1).Players.Single().Name);
        Assert.Equal(1, game.Score!.Winner);
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
