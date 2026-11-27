namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Player's free days in their League's current Stage (#9).</summary>
/// <param name="Stage">The Stage they're for: its days are the only ones that can be ticked.</param>
/// <param name="Dates">The days they're free, earliest first; none until they say.</param>
/// <param name="LastDay">The last day that can be ticked: the Stage's end, or the League's end once the Stage is past its deadline and still open.</param>
public record GetAvailabilityResponse(LeagueStageSummary Stage, IReadOnlyList<DateOnly> Dates, DateOnly LastDay);
