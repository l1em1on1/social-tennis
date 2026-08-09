using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Auth;
using SocialTennis.Api.Features.Clubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TennisDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddOpenApi();

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddScoped<IMagicLinkSender, LoggingMagicLinkSender>();
builder.Services
    .AddAuthentication(SessionTokenAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionTokenAuthenticationHandler>(
        SessionTokenAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();

var app = builder.Build();

// Schema is applied by the app itself so `docker compose up` needs no separate
// migration step (ADR-0005). EF Core guidance endorses this for dev/test and
// single-instance deployments — which v1 is; move to scripted migrations
// (`dotnet ef migrations script`) before scaling to multiple API instances.
// The IsDesignTime guard keeps `dotnet ef` tooling from triggering it.
//
// EF 9+'s own migration lock lives inside the target database, so concurrent
// hosts can still race on first-run creation of that database and its tables
// (observed in CI: parallel test hosts, 42P07 "Users already exists"). An
// advisory lock on the always-present "postgres" maintenance database
// serializes the whole step; it releases with the connection.
if (!EF.IsDesignTime)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TennisDbContext>();

    var lockConnectionString = new NpgsqlConnectionStringBuilder(
        db.Database.GetConnectionString())
    {
        Database = "postgres",
        // Advisory locks are session-scoped and Npgsql's pool reset does not
        // release them — a pooled return would hold the lock until idle
        // pruning (~5 min). Unpooled, disposal closes the session and frees
        // the lock immediately.
        Pooling = false,
    }.ConnectionString;

    await using var lockConnection = new NpgsqlConnection(lockConnectionString);
    await lockConnection.OpenAsync();
    await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(727274201)", lockConnection))
    {
        // Waiting for another host's first-run migrate can exceed the default
        // 30s command timeout — the wait itself must never time out.
        acquire.CommandTimeout = 0;
        await acquire.ExecuteNonQueryAsync();
    }

    await db.Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();

// Served unconditionally: the TS client is generated from /openapi/v1.json
// inside the compose network (see web's api:generate script).
app.MapOpenApi();

// Registration order is reflected in the OpenAPI document's path order, and
// therefore in the generated TS client — keep it stable to keep client diffs
// meaningful.
app.MapClubsEndpoints();
app.MapAuthEndpoints();

app.Run();

// Exposes the entry point to WebApplicationFactory in integration tests.
public partial class Program;
