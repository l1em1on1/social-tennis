namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Team in a League, as a Manager sees it (#6).</summary>
/// <param name="PlayerIds">Its Players, as in the League's participants: one in singles, two in doubles.</param>
/// <param name="Rating">
/// The average of its Players' Ratings, worked out on read; a Team has no
/// Rating of its own (CONTEXT.md, Team).
/// </param>
public record LeagueTeamSummary(Guid Id, IReadOnlyList<Guid> PlayerIds, double Rating);
