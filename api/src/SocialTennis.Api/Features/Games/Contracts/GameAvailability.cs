namespace SocialTennis.Api.Features.Games.Contracts;

/// <summary>
/// The days that suit everyone in a Game (#9, #276), for its own Players
/// only: from today (or the Stage's start, if later) on, the days every one
/// of them is free, by their own setting for the day or else their usual
/// week, with the time they all share. Those run to the Stage's end, or the
/// League's end once a Stage is held past its deadline.
/// </summary>
/// <param name="Shared">Earliest first; none if no day suits everyone yet. A day whose times don't overlap isn't one.</param>
/// <param name="NotGiven">The Players in it with no usual week and no day of their own set yet, by name: until they say, no day can suit everyone.</param>
public record GameAvailability(IReadOnlyList<SharedTime> Shared, IReadOnlyList<string> NotGiven);
