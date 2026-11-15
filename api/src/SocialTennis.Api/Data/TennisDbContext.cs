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

    public DbSet<Player> Players => Set<Player>();

    public DbSet<ClubMembership> ClubMemberships => Set<ClubMembership>();

    public DbSet<ClubInvite> ClubInvites => Set<ClubInvite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Club>(club =>
        {
            // No seeded Club: an Admin creates it (issue #18).
            club.Property(c => c.Name).HasMaxLength(Club.NameMaxLength);
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

        modelBuilder.Entity<Player>(player =>
        {
            player.Property(p => p.Name).HasMaxLength(Player.NameMaxLength);
            player.HasOne(p => p.User).WithMany().OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClubMembership>(membership =>
        {
            membership.HasKey(m => new { m.PlayerId, m.ClubId });
            membership.HasOne(m => m.Player).WithMany(p => p.Memberships).OnDelete(DeleteBehavior.Cascade);
            membership.HasOne(m => m.Club).WithMany().OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClubInvite>(invite =>
        {
            invite.Property(i => i.Email).HasMaxLength(320);
            invite.HasIndex(i => new { i.ClubId, i.Email }).IsUnique();
            invite.HasIndex(i => i.Email);
            invite.HasOne(i => i.Club).WithMany().OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SessionToken>(token =>
        {
            token.Property(t => t.TokenHash).HasMaxLength(64);
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasOne(t => t.User).WithMany().OnDelete(DeleteBehavior.Cascade);
        });
    }
}
