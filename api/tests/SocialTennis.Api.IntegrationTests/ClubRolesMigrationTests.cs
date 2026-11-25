using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// The migration that moves Managers into Clubs (#110), on a database of its
/// own at the migration before it: the shared one is only ever at the latest.
/// </summary>
public class ClubRolesMigrationTests(AuthTestFactory factory) : IClassFixture<AuthTestFactory>
{
    private const string Before = "20261124234733_UserLastSignedIn";

    private const int Manager = 1;
    private const int Admin = 2;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Every_Manager_becomes_a_manager_of_every_Club_there_is_and_back(int clubs)
    {
        await using var world = await DatabaseAsync();
        var db = world.Db;
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Before);
        var clubIds = Enumerable.Range(0, clubs).Select(_ => Guid.NewGuid()).ToList();
        foreach (var id in clubIds)
        {
            await db.Database.ExecuteSqlAsync($"""INSERT INTO "Clubs" ("Id", "Name") VALUES ({id}, {"Club " + id})""");
        }

        async Task<Guid> UserAsync(int roles)
        {
            var id = Guid.NewGuid();
            await db.Database.ExecuteSqlAsync(
                $"""INSERT INTO "Users" ("Id", "Email", "CreatedAt", "Roles") VALUES ({id}, {id + "@example.test"}, now(), {roles})""");
            return id;
        }

        var manager = await UserAsync(Manager);
        var both = await UserAsync(Manager | Admin);
        var admin = await UserAsync(Admin);
        var player = await UserAsync(0);

        await migrator.MigrateAsync();

        var held = await db.ClubRoles.Select(r => new { r.UserId, r.ClubId, r.Kind }).ToListAsync();
        Assert.All(held, r => Assert.Equal(ClubRoleKind.Manager, r.Kind));
        Assert.Equal(
            new[] { manager, both }.SelectMany(u => clubIds.Select(c => (u, c))).Order(),
            held.Select(r => (r.UserId, r.ClubId)).Order());
        Assert.Equal(
            new Dictionary<Guid, UserRoles> { [manager] = 0, [both] = UserRoles.Admin, [admin] = UserRoles.Admin, [player] = 0 }.OrderBy(r => r.Key),
            (await db.Users.ToDictionaryAsync(u => u.Id, u => u.Roles)).OrderBy(r => r.Key));

        await migrator.MigrateAsync(Before);

        // Back: a manager of any Club is a Manager again, and nobody else is.
        var back = await db.Database.SqlQuery<Guid>($"""SELECT "Id" AS "Value" FROM "Users" WHERE ("Roles" & 1) = 1""").ToListAsync();
        Assert.Equal((clubs > 0 ? new[] { manager, both } : []).Order(), back.Order());
    }

    private sealed class World(TennisDbContext db, string server, string name) : IAsyncDisposable
    {
        public TennisDbContext Db => db;

        public async ValueTask DisposeAsync()
        {
            await db.DisposeAsync();
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(server);
            await connection.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", connection);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private async Task<World> DatabaseAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var shared = scope.ServiceProvider.GetRequiredService<TennisDbContext>().Database.GetConnectionString();
        var name = $"tennis_test_migration_{Guid.NewGuid():N}";
        var own = new NpgsqlConnectionStringBuilder(shared) { Database = name, Pooling = false }.ConnectionString;
        var server = new NpgsqlConnectionStringBuilder(shared) { Database = "postgres", Pooling = false }.ConnectionString;
        var options = new DbContextOptionsBuilder<TennisDbContext>().UseNpgsql(own).Options;
        return new World(new TennisDbContext(options), server, name);
    }
}
