namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Player's place on a started League's ladder, as they see it on their Leagues page (#7).</summary>
/// <param name="Division">Their Division, 1 at the top.</param>
/// <param name="Opponents">The other Teams in it, higher on the ladder first: they play each once this Stage (ADR-0031).</param>
public record PlayerLeaguePlace(int Division, IReadOnlyList<PlayerLeagueOpponent> Opponents);
