namespace SocialTennis.Api.Domain;

/// <summary>
/// A Player asking to enter a League (see CONTEXT.md, Join Request). One per
/// Player and League: it is kept once decided, so both sides can see the
/// outcome, and a Player can't ask again after a rejection. A Manager can
/// still add them directly. In a doubles League it may name a partner, and
/// approving it puts both in as a Team (#6).
/// </summary>
public class LeagueJoinRequest
{
    public Guid LeagueId { get; set; }

    public League League { get; set; } = null!;

    public Guid PlayerId { get; set; }

    public Player Player { get; set; } = null!;

    /// <summary>
    /// The partner the Player asks to join with, in a doubles League; null for
    /// a Player asking alone. The partner didn't ask: a rejection stops only
    /// the Player who did.
    /// </summary>
    public Guid? PartnerId { get; set; }

    public Player? Partner { get; set; }

    public LeagueJoinRequestStatus Status { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>When a Manager approved or rejected it; null while pending.</summary>
    public DateTimeOffset? DecidedAt { get; set; }
}
