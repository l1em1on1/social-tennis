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

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<Stage> Stages => Set<Stage>();

    public DbSet<LadderPlace> LadderPlaces => Set<LadderPlace>();

    public DbSet<Game> Games => Set<Game>();

    public DbSet<GameSide> GameSides => Set<GameSide>();

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
            // Through the League's id too, so a participant's Team is always of
            // their own League. A null TeamId makes the whole key null (EF Core
            // docs, "composite foreign key"): an unpaired Player. No cascade:
            // breaking a Team up clears its members' TeamId first (LeagueTeams).
            participant.HasOne(p => p.Team).WithMany(t => t.Members)
                .HasForeignKey(p => new { p.LeagueId, p.TeamId })
                .HasPrincipalKey(t => new { t.LeagueId, t.Id })
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Team>(team =>
        {
            team.HasOne(t => t.League).WithMany(l => l.Teams).OnDelete(DeleteBehavior.Cascade);
        });

        // The ladder (#7, ADR-0030). Every row below carries its League's id, so
        // composite keys keep a Stage's places, Games and Sides, and the Teams
        // they name, in one League. A Team on a ladder or in a Game can't be
        // deleted (NoAction): the handlers refuse first (LeagueLadder).
        modelBuilder.Entity<Stage>(stage =>
        {
            stage.HasOne(s => s.League).WithMany(l => l.Stages).OnDelete(DeleteBehavior.Cascade);
            stage.HasAlternateKey(s => new { s.LeagueId, s.Id });
            // One Stage of each number: a second start can't open another Stage 1.
            stage.HasIndex(s => new { s.LeagueId, s.Number }).IsUnique();
        });
        modelBuilder.Entity<LadderPlace>(place =>
        {
            place.HasKey(p => new { p.StageId, p.Position });
            place.HasIndex(p => new { p.StageId, p.TeamId }).IsUnique();
            place.HasOne(p => p.Stage).WithMany(s => s.Places)
                .HasForeignKey(p => new { p.LeagueId, p.StageId })
                .HasPrincipalKey(s => new { s.LeagueId, s.Id })
                .OnDelete(DeleteBehavior.Cascade);
            place.HasOne(p => p.Team).WithMany()
                .HasForeignKey(p => new { p.LeagueId, p.TeamId })
                .HasPrincipalKey(t => new { t.LeagueId, t.Id })
                .OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<Game>(game =>
        {
            game.HasAlternateKey(g => new { g.LeagueId, g.Id });
            game.HasOne(g => g.Stage).WithMany(s => s.Games)
                .HasForeignKey(g => new { g.LeagueId, g.StageId })
                .HasPrincipalKey(s => new { s.LeagueId, s.Id })
                .OnDelete(DeleteBehavior.Cascade);
            game.HasIndex(g => new { g.StageId, g.Division });
        });
        modelBuilder.Entity<GameSide>(side =>
        {
            side.HasKey(s => new { s.GameId, s.Side });
            side.HasOne(s => s.Game).WithMany(g => g.Sides)
                .HasForeignKey(s => new { s.LeagueId, s.GameId })
                .HasPrincipalKey(g => new { g.LeagueId, g.Id })
                .OnDelete(DeleteBehavior.Cascade);
            side.HasOne(s => s.Team).WithMany()
                .HasForeignKey(s => new { s.LeagueId, s.TeamId })
                .HasPrincipalKey(t => new { t.LeagueId, t.Id })
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<LeagueJoinRequest>(request =>
        {
            // One per Player and League, decided or not: a second ask is refused.
            request.HasKey(r => new { r.LeagueId, r.PlayerId });
            request.HasOne(r => r.League).WithMany(l => l.JoinRequests).OnDelete(DeleteBehavior.Cascade);
            request.HasOne(r => r.Player).WithMany().HasForeignKey(r => r.PlayerId).OnDelete(DeleteBehavior.Cascade);
            // The partner a doubles request names (#6): it goes with them, as with the asker.
            request.HasOne(r => r.Partner).WithMany().HasForeignKey(r => r.PartnerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SessionToken>(token =>
        {
            token.Property(t => t.TokenHash).HasMaxLength(64);
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasOne(t => t.User).WithMany().OnDelete(DeleteBehavior.Cascade);
        });
    }
}
