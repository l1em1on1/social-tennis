using SocialTennis.Api.Validation;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A date a Player sets differently from their usual week (#276): not free, free any time, or free in a window.</summary>
/// <param name="Date">The date, inside the Stage.</param>
/// <param name="Free">Whether they're free that day.</param>
/// <param name="From">From when, with To; neither is any time. None on a day not free.</param>
/// <param name="To">Until when, after From.</param>
public record AvailabilityDaySetting(DateOnly Date, bool Free, TimeOnly? From, TimeOnly? To)
{
    /// <summary>What's wrong with it, or none.</summary>
    internal string? Problem() =>
        Free ? TimeRules.WindowProblem(From, To)
        : From is null && To is null ? null
        : "A day you're not free has no times.";
}
