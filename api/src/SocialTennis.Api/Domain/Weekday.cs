using System.Text.Json.Serialization;

namespace SocialTennis.Api.Domain;

/// <summary>
/// A day of the week, Monday first (ISO 8601), as a Player's usual week has
/// them (#276). Not <see cref="DayOfWeek"/>, whose Sunday is 0: stored as
/// 1 to 7, so the week orders as members read it.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<Weekday>))]
public enum Weekday
{
    Monday = 1,
    Tuesday = 2,
    Wednesday = 3,
    Thursday = 4,
    Friday = 5,
    Saturday = 6,
    Sunday = 7,
}

public static class Weekdays
{
    /// <summary>The weekday a date falls on.</summary>
    public static Weekday Of(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday ? Weekday.Sunday : (Weekday)(int)date.DayOfWeek;
}
