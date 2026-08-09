using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Data;

public class TennisDbContext(DbContextOptions<TennisDbContext> options) : DbContext(options)
{
    public DbSet<Club> Clubs => Set<Club>();

    public DbSet<User> Users => Set<User>();

    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();

    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();

    public DbSet<SessionToken> SessionTokens => Set<SessionToken>();

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

        modelBuilder.Entity<User>(user =>
        {
            user.Property(u => u.Email).HasMaxLength(320);
            user.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<ExternalLogin>(login =>
        {
            login.Property(l => l.Provider).HasMaxLength(100);
            login.Property(l => l.Subject).HasMaxLength(300);
            login.HasIndex(l => new { l.Provider, l.Subject }).IsUnique();
            login.HasOne(l => l.User).WithMany(u => u.ExternalLogins).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MagicLinkToken>(token =>
        {
            token.Property(t => t.TokenHash).HasMaxLength(64);
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasOne(t => t.User).WithMany().OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SessionToken>(token =>
        {
            token.Property(t => t.TokenHash).HasMaxLength(64);
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasOne(t => t.User).WithMany().OnDelete(DeleteBehavior.Cascade);
        });
    }
}
