namespace SocialTennis.Api.Domain;

/// <summary>The Club's calendar (ADR-0016): what day it is where the Club is.</summary>
public static class ClubCalendar
{
    /// <summary>Today's date in the Club's <paramref name="timeZone"/>, by the injected clock.</summary>
    public static DateOnly Today(string timeZone, TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime);
}
