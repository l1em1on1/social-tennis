namespace SocialTennis.Api.Domain;

/// <summary>
/// A date in a Stage that a Player has set themselves (#9, #276; CONTEXT.md,
/// Availability): not free, free any time, or free from <see cref="From"/>
/// to <see cref="To"/>. A date with no row follows the Player's usual week
/// (<see cref="UsualDay"/>). The dates every Player in a Game is free, with
/// the times they share, are suggested for its Date. It's the Player's alone:
/// deleting their account deletes it (<c>AccountDeletion</c>).
/// </summary>
public class AvailableDay
{
    public Guid StageId { get; set; }

    public Stage Stage { get; set; } = null!;

    public Guid PlayerId { get; set; }

    public Player Player { get; set; } = null!;

    public DateOnly Date { get; set; }

    /// <summary>
    /// Not free that date, though the usual week may say otherwise. False, the
    /// CLR default, is free: so the days ticked before #276 stay free any time.
    /// </summary>
    public bool NotFree { get; set; }

    /// <summary>From when, with <see cref="To"/>; none with none is any time. None on a date not free.</summary>
    public TimeOnly? From { get; set; }

    /// <summary>Until when, after <see cref="From"/>.</summary>
    public TimeOnly? To { get; set; }
}
