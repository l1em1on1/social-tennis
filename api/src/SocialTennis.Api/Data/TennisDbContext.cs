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

    public DbSet<League> Leagues => Set<League>();

    public DbSet<LeagueParticipant> LeagueParticipants => Set<LeagueParticipant>();

    public DbSet<LeagueJoinRequest> LeagueJoinRequests => Set<LeagueJoinRequest>();

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
            // Same length as User.Email. No foreign key: the User may not exist yet (#108).
            token.Property(t => t.Email).HasMaxLength(320);
            token.Property(t => t.ClientHash).HasMaxLength(64);
            token.HasIndex(t => t.Email);
        });

        modelBuilder.Entity<Player>(player =>
        {
            player.Property(p => p.Name).HasMaxLength(Player.NameMaxLength);
            // Rows that predate the column are Players too, and none is unrated.
            player.Property(p => p.Rating).HasDefaultValue(Player.DefaultRating);
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

        modelBuilder.Entity<League>(league =>
        {
            league.Property(l => l.Name).HasMaxLength(League.NameMaxLength);
            // Complex types: columns on the Leagues row, not tables of their own
            // (EF Core docs, "Complex types", table splitting).
            league.ComplexProperty(l => l.StageLength);
            league.ComplexProperty(l => l.ScoreFormat);
            league.HasIndex(l => l.ClubId);
            league.HasOne(l => l.Club).WithMany().OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeagueParticipant>(participant =>
        {
            participant.HasKey(p => new { p.LeagueId, p.PlayerId });
            participant.HasOne(p => p.League).WithMany(l => l.Participants).OnDelete(DeleteBehavior.Cascade);
            participant.HasOne(p => p.Player).WithMany().OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeagueJoinRequest>(request =>
        {
            // One per Player and League, decided or not: a second ask is refused.
            request.HasKey(r => new { r.LeagueId, r.PlayerId });
            request.HasOne(r => r.League).WithMany(l => l.JoinRequests).OnDelete(DeleteBehavior.Cascade);
            request.HasOne(r => r.Player).WithMany().OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SessionToken>(token =>
        {
            token.Property(t => t.TokenHash).HasMaxLength(64);
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasOne(t => t.User).WithMany().OnDelete(DeleteBehavior.Cascade);
        });
    }
}
