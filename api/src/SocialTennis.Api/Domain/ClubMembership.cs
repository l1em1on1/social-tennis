namespace SocialTennis.Api.Domain;

/// <summary>
/// A Player belonging to a Club. A Player may belong to more than one Club,
/// though in practice it is usually one (see CONTEXT.md).
/// </summary>
public class ClubMembership
{
    public Guid PlayerId { get; set; }

    public Player Player { get; set; } = null!;

    public Guid ClubId { get; set; }

    public Club Club { get; set; } = null!;

    public DateTimeOffset JoinedAt { get; set; }
}
