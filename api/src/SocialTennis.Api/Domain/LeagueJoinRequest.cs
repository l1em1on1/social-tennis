namespace SocialTennis.Api.Domain;

/// <summary>
/// A Player asking to enter a League (see CONTEXT.md, Join Request). One per
/// Player and League: it is kept once decided, so both sides can see the
/// outcome, and a Player can't ask again after a rejection. A Manager can
/// still add them directly.
/// </summary>
public class LeagueJoinRequest
{
    public Guid LeagueId { get; set; }

    public League League { get; set; } = null!;

    public Guid PlayerId { get; set; }

    public Player Player { get; set; } = null!;

    public LeagueJoinRequestStatus Status { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>When a Manager approved or rejected it; null while pending.</summary>
    public DateTimeOffset? DecidedAt { get; set; }
}
