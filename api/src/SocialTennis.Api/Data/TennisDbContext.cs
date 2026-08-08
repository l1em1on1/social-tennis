using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Data;

public class TennisDbContext(DbContextOptions<TennisDbContext> options) : DbContext(options)
{
    public DbSet<Club> Clubs => Set<Club>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Club>(club =>
        {
            club.Property(c => c.Name).HasMaxLength(200);

            // Walking-skeleton seed: one Club proves the whole path
            // (migration -> Postgres -> API -> OpenAPI -> BFF -> page).
            club.HasData(new Club
            {
                Id = Guid.Parse("6edb14f9-1b3d-4a63-b998-0d5757a5c8f1"),
                Name = "Social Tennis Club",
            });
        });
    }
}
