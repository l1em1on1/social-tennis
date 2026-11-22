namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>The next Stage's dates as proposed: from the day after this one ends, for one Stage length (#10).</summary>
public record CloseNextStage(DateOnly StartDate, DateOnly EndDate);
