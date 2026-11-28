namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Player's days in their League's current Stage (#9, #276).</summary>
/// <param name="Stage">The Stage they're for: its days are the only ones that can be set.</param>
/// <param name="Days">
/// Every date from the Stage's start to <paramref name="LastDay"/>, earliest first, with what counts on it: the
/// Player's own setting for it, else their usual week. With neither, a date isn't free.
/// </param>
/// <param name="LastDay">The last day that can be set: the Stage's end, or the League's end once the Stage is past its deadline and still open.</param>
public record GetAvailabilityResponse(LeagueStageSummary Stage, IReadOnlyList<AvailabilityDay> Days, DateOnly LastDay);
