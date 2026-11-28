using SocialTennis.Api.Domain;
using SocialTennis.Api.Validation;

namespace SocialTennis.Api.Features.Players.Contracts;

/// <summary>A weekday a Player is usually free (#276): any time, or from and to.</summary>
/// <param name="Weekday">Which day of the week.</param>
/// <param name="From">From when ("18:00"), with To; neither is any time.</param>
/// <param name="To">Until when, after From.</param>
public record UsualWeekDay(Weekday Weekday, TimeOnly? From, TimeOnly? To)
{
    /// <summary>What's wrong with it, or none.</summary>
    internal string? Problem() =>
        Enum.IsDefined(Weekday) ? TimeRules.WindowProblem(From, To) : "Pick a day of the week, Monday to Sunday.";
}
