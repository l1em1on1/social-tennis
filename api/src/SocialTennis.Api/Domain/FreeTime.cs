namespace SocialTennis.Api.Domain;

/// <summary>
/// When a Player is free on a day (#276): any time (neither end), or from
/// <see cref="From"/> to <see cref="To"/>, the same day.
/// </summary>
public readonly record struct FreeWindow(TimeOnly? From, TimeOnly? To)
{
    public static FreeWindow AnyTime => default;

    /// <summary>
    /// The time both windows share, or none if they don't overlap. Windows
    /// that only touch ("18:00–20:00" and "20:00–22:00") leave no time to play.
    /// </summary>
    public FreeWindow? Overlap(FreeWindow other)
    {
        if (From is not { } from || To is not { } to)
        {
            return other;
        }

        if (other.From is not { } otherFrom || other.To is not { } otherTo)
        {
            return this;
        }

        var start = from > otherFrom ? from : otherFrom;
        var end = to < otherTo ? to : otherTo;
        return start < end ? new FreeWindow(start, end) : null;
    }
}

/// <summary>
/// What a Player has said about their time in a Stage (#276; CONTEXT.md,
/// Availability): the dates they set themselves, and their usual week, which
/// stands for every other date.
/// </summary>
/// <param name="Own">The dates they set: free in a window, or none for not free.</param>
/// <param name="Usual">The weekdays they're usually free; a weekday not listed is one they usually aren't.</param>
public sealed record PlayerFreeTime(IReadOnlyDictionary<DateOnly, FreeWindow?> Own, IReadOnlyDictionary<Weekday, FreeWindow> Usual)
{
    public static PlayerFreeTime Of(IEnumerable<AvailableDay> own, IEnumerable<UsualDay> usual) =>
        new(
            own.ToDictionary(d => d.Date, d => d.NotFree ? (FreeWindow?)null : new FreeWindow(d.From, d.To)),
            usual.ToDictionary(d => d.Weekday, d => new FreeWindow(d.From, d.To)));

    /// <summary>
    /// Whether they've said anything: a usual week, or a date of their own. Until
    /// they do, no date can suit everyone in their Games.
    /// </summary>
    public bool Given => Own.Count > 0 || Usual.Count > 0;

    /// <summary>What counts on a date: their own setting for it, else their usual week. None: not free.</summary>
    public FreeWindow? On(DateOnly date) =>
        Own.TryGetValue(date, out var own) ? own
        : Usual.TryGetValue(Weekdays.Of(date), out var usual) ? usual
        : null;
}

/// <summary>The days that suit everyone in a Game (#9, #276).</summary>
public static class FreeTime
{
    /// <summary>
    /// The dates from <paramref name="first"/> to <paramref name="last"/> that every one
    /// of <paramref name="players"/> is free, with the time they all share. A date
    /// whose windows don't overlap isn't one. No Players, no dates.
    /// </summary>
    public static IReadOnlyList<(DateOnly Date, FreeWindow Window)> Shared(
        IReadOnlyCollection<PlayerFreeTime> players, DateOnly first, DateOnly last)
    {
        var shared = new List<(DateOnly, FreeWindow)>();
        if (players.Count == 0)
        {
            return shared;
        }

        for (var date = first; date <= last; date = date.AddDays(1))
        {
            FreeWindow? window = FreeWindow.AnyTime;
            foreach (var player in players)
            {
                window = player.On(date) is { } theirs ? window.Value.Overlap(theirs) : null;
                if (window is null)
                {
                    break;
                }
            }

            if (window is { } everyone)
            {
                shared.Add((date, everyone));
            }
        }

        return shared;
    }
}
