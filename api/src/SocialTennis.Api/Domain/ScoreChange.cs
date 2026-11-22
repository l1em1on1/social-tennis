namespace SocialTennis.Api.Domain;

/// <summary>
/// One entry in a Score's history (see CONTEXT.md, Score): who changed it,
/// when, and from what to what. Kept for good, and shown to the League's
/// Players; in the first release it stands in for Reactions (ADR-0008).
/// </summary>
public class ScoreChange
{
    public Guid Id { get; set; }

    public Guid LeagueId { get; set; }

    public Guid GameId { get; set; }

    public Game Game { get; set; } = null!;

    public ScoreChangeKind Kind { get; set; }

    public DateTimeOffset At { get; set; }

    /// <summary>The signed-in User who made it.</summary>
    public Guid ByUserId { get; set; }

    public User ByUser { get; set; } = null!;

    /// <summary>The Player they acted as, one who played the Game; none for a Manager's override.</summary>
    public Guid? ByPlayerId { get; set; }

    public Player? ByPlayer { get; set; }

    /// <summary>None when the Score was entered.</summary>
    public ScoreValue? Before { get; set; }

    public required ScoreValue After { get; set; }
}
