namespace SocialTennis.Api.Domain;

/// <summary>
/// A User's role in one Club (#110; CONTEXT.md, Club manager). One per Club and
/// User, and as many Clubs as they manage. Deleted with either. An Admin needs
/// none: they can act in every Club.
/// </summary>
public class ClubRole
{
    public Guid ClubId { get; set; }

    public Club Club { get; set; } = null!;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public ClubRoleKind Kind { get; set; }

    public DateTimeOffset GrantedAt { get; set; }
}
