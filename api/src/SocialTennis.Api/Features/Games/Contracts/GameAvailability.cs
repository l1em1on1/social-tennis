namespace SocialTennis.Api.Features.Games.Contracts;

/// <summary>
/// The days that suit everyone in a Game (#9), for its own Players only: from
/// today on, the days every one of them said they're free. Those run to the
/// Stage's end, or the League's end once a Stage is held past its deadline.
/// </summary>
/// <param name="SharedDates">Earliest first; none if no day suits everyone yet.</param>
/// <param name="NotGiven">The Players in it who haven't said any day yet, by name: until they do, no day can suit everyone.</param>
public record GameAvailability(IReadOnlyList<DateOnly> SharedDates, IReadOnlyList<string> NotGiven);
