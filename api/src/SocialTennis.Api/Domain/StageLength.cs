namespace SocialTennis.Api.Domain;

/// <summary>
/// How long each Stage of a League lasts: the window Players get to arrange
/// and play their Games (see CONTEXT.md, Stage). For a Divisional League it is
/// the Relegation cadence, usually one calendar month; for a Knockout League,
/// the window each Bracket round gets.
/// </summary>
public record StageLength(int Count, StageLengthUnit Unit)
{
    public const int MinCount = 1;

    public const int MaxCount = 12;
}
