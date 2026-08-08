using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TennisDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddOpenApi();

var app = builder.Build();

// Schema is applied by the app itself so `docker compose up` needs no
// separate migration step (ADR-0005).
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<TennisDbContext>().Database.Migrate();
}

// Served unconditionally: the TS client is generated from /openapi/v1.json
// inside the compose network (see web's api:generate script).
app.MapOpenApi();

app.MapGet("/clubs", async (TennisDbContext db) => await db.Clubs.OrderBy(c => c.Name).ToListAsync())
    .WithName("GetClubs");

app.Run();

// Exposes the entry point to WebApplicationFactory in integration tests.
public partial class Program;
