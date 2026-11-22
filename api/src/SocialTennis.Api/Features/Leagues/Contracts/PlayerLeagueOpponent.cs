namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Team a Player plays in their Division this Stage (#7), by its Players' names.</summary>
public record PlayerLeagueOpponent(Guid TeamId, IReadOnlyList<string> Names);
