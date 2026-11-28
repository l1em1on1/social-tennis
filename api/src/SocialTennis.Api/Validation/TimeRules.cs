namespace SocialTennis.Api.Validation;

/// <summary>
/// Rules every time a Player gives shares (#276): a usual weekday's window, a
/// date's, and a Game's start time. The database checks a window's ends too.
/// </summary>
public static class TimeRules
{
    /// <summary>What's wrong with a window, or none: any time (neither end), or both, in whole minutes, the second after the first.</summary>
    public static string? WindowProblem(TimeOnly? from, TimeOnly? to) => (from, to) switch
    {
        (null, null) => null,
        (null, _) or (_, null) => "Give a window both a from and a to time, or neither for any time.",
        ({ } start, { } end) when !WholeMinutes(start) || !WholeMinutes(end) => WholeMinutesMessage,
        ({ } start, { } end) when start >= end => "A window ends after it starts, on the same day.",
        _ => null,
    };

    public static bool WholeMinutes(TimeOnly time) => time.Ticks % TimeSpan.TicksPerMinute == 0;

    public const string WholeMinutesMessage = "Give times in whole minutes, like 18:30.";
}
