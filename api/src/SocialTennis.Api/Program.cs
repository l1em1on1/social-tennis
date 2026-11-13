using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Auth;
using SocialTennis.Api.Features.Clubs;
using SocialTennis.Api.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TennisDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddOpenApi(options => options.AddOperationTransformer<UnauthorizedResponseTransformer>());

// One clock for the whole app. Nothing reads DateTimeOffset.UtcNow directly, so
// a test can replace this and stand either side of a deadline (ADR-0016).
builder.Services.AddSingleton(TimeProvider.System);

// Every error response gets an RFC 9457 body: unhandled exceptions through
// UseExceptionHandler, bodiless 4xx/5xx (the auth 401, the rate limiter's 429)
// through UseStatusCodePages (ADR-0017).
builder.Services.AddProblemDetails();

// Tagged, so /health/ready runs it and /health doesn't (see the mappings below).
builder.Services.AddHealthChecks().AddDbContextCheck<TennisDbContext>(tags: [ReadyTag]);

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddScoped<IMagicLinkSender, LoggingMagicLinkSender>();
builder.Services
    .AddAuthentication(SessionTokenAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionTokenAuthenticationHandler>(
        SessionTokenAuthenticationHandler.SchemeName, null);

// Deny by default (ADR-0017): an endpoint that declares nothing requires a
// signed-in User. Anonymous routes opt out with AllowAnonymous(), so a
// forgotten declaration fails closed (ASP.NET Core docs, "fallback policy").
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// A ceiling on magic-link requests across all callers. It is not per client:
// every request arrives from the BFF, so the API never sees a browser's
// address. Per-address throttling is in RequestMagicLink; per-client limiting
// belongs at the edge, where the client's address is known.
// Configured through IOptions so the limit is read when the limiter is first
// used, after test hosts have applied their configuration overrides.
builder.Services.AddRateLimiter(_ => { });
builder.Services.AddOptions<RateLimiterOptions>().Configure<IOptions<AuthOptions>>((limiter, auth) =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.AddFixedWindowLimiter(AuthOptions.MagicLinkRateLimitPolicy, window =>
    {
        window.PermitLimit = auth.Value.MagicLinkRequestsPerMinute;
        window.Window = TimeSpan.FromMinutes(1);
        window.QueueLimit = 0;
    });
});

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

    // The lock database defaults to "postgres" because on a first run the
    // app's own database may not exist yet. A hosted Postgres (Neon, ADR-0019)
    // creates the app database up front and may not let the app's role into
    // "postgres"; there, set Database:MigrationLockDatabase to the app database.
    var lockConnectionString = new NpgsqlConnectionStringBuilder(
        db.Database.GetConnectionString())
    {
        Database = app.Configuration["Database:MigrationLockDatabase"] ?? "postgres",
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

// The developer exception page already answers API clients with problem
// details; everywhere else an unhandled exception becomes a bare 500 problem.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Two probes, both anonymous (callers have no session):
//   /health        the process is up. Runs no checks, so it never touches the
//                  database. For a host's liveness probe: on a scale-to-zero
//                  database, a probe that queries it every few seconds keeps
//                  it awake and spends its compute allowance (ADR-0019).
//   /health/ready  up, migrated, and able to reach Postgres. For compose
//                  healthchecks and the E2E runner.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(ReadyTag),
}).AllowAnonymous();

// Served unconditionally: the TS client is generated from /openapi/v1.json
// inside the compose network (see web's api:generate script). Anonymous for
// the same reason; the api service publishes no port (ADR-0001).
app.MapOpenApi().AllowAnonymous();

// Registration order is reflected in the OpenAPI document's path order, and
// therefore in the generated TS client — keep it stable to keep client diffs
// meaningful.
app.MapClubsEndpoints();
app.MapAuthEndpoints();

app.Run();

// Exposes the entry point to WebApplicationFactory in integration tests.
public partial class Program
{
    /// <summary>Tag on health checks that /health/ready runs and /health skips.</summary>
    public const string ReadyTag = "ready";
}
