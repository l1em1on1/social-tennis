using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Auth;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

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
if (!EF.IsDesignTime)
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<TennisDbContext>().Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();

// Served unconditionally: the TS client is generated from /openapi/v1.json
// inside the compose network (see web's api:generate script).
app.MapOpenApi();

app.MapGet("/clubs", async (TennisDbContext db) => await db.Clubs.OrderBy(c => c.Name).ToListAsync())
    .WithName("GetClubs");

app.MapAuthEndpoints();

app.Run();

// Exposes the entry point to WebApplicationFactory in integration tests.
public partial class Program;
