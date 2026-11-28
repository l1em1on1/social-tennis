namespace SocialTennis.Api.Features.Players.Contracts;

/// <summary>
/// A Player's usual week (#276): the weekdays they're usually free, Monday
/// first. It stands for every date of every Stage they haven't set
/// themselves, in every League.
/// </summary>
/// <param name="Days">The weekdays they're usually free; a weekday not listed is one they usually aren't. None until they say.</param>
public record UsualWeek(IReadOnlyList<UsualWeekDay> Days);
