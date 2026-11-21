namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Competitor on the proposed starting ladder: a Team, with its Players (#7).</summary>
/// <param name="Rating">The average of its Players' Ratings, which the proposal is ordered by.</param>
/// <param name="JoinedAt">Its earliest Player's join time, which breaks ties in Rating.</param>
public record LeagueStartCompetitor(Guid TeamId, IReadOnlyList<LeagueStartPlayer> Players, double Rating, DateTimeOffset JoinedAt);
