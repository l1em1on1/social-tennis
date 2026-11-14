namespace SocialTennis.Api.Domain;

/// <summary>
/// A Player in a Club, or asking to be. A Player may belong to more than one
/// Club, though in practice it is usually one (see CONTEXT.md).
/// </summary>
public class ClubMembership
{
    public Guid PlayerId { get; set; }

    public Player Player { get; set; } = null!;

    public Guid ClubId { get; set; }

    public Club Club { get; set; } = null!;

    public ClubMembershipStatus Status { get; set; }

    /// <summary>When the Player asked to join, or was let in by an invite.</summary>
    public DateTimeOffset RequestedAt { get; set; }
}
