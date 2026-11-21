namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A League's current Stage, or the first one a start would open (#7).</summary>
/// <param name="EndDate">The deadline: it passes at the end of this day, in the Club's time zone (ADR-0016).</param>
public record LeagueStageSummary(int Number, DateOnly StartDate, DateOnly EndDate);
