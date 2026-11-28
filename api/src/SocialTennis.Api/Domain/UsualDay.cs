namespace SocialTennis.Api.Domain;

/// <summary>
/// A weekday a Player is usually free, in their usual week (#276; CONTEXT.md,
/// Availability): any time, or from <see cref="From"/> to <see cref="To"/>.
/// There's no row for a weekday they usually aren't free. It stands for every
/// date of every Stage the Player hasn't set (<see cref="AvailableDay"/>), in
/// every League. It's the Player's alone: deleting their account deletes it
/// (<c>AccountDeletion</c>).
/// </summary>
public class UsualDay
{
    public Guid PlayerId { get; set; }

    public Player Player { get; set; } = null!;

    public Weekday Weekday { get; set; }

    /// <summary>From when, with <see cref="To"/>; none with none is any time.</summary>
    public TimeOnly? From { get; set; }

    /// <summary>Until when, after <see cref="From"/>.</summary>
    public TimeOnly? To { get; set; }
}
