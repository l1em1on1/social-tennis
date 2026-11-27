namespace SocialTennis.Api.Domain;

/// <summary>
/// A day a Player is free to play in a Stage (#9; CONTEXT.md, Availability):
/// a whole day, from the Stage's start to its end (owner, 2026-11-27). The
/// days every Player in a Game is free are suggested for its Date. It's the
/// Player's, and goes with them.
/// </summary>
public class AvailableDay
{
    public Guid StageId { get; set; }

    public Stage Stage { get; set; } = null!;

    public Guid PlayerId { get; set; }

    public Player Player { get; set; } = null!;

    public DateOnly Date { get; set; }
}
