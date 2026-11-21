namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Player's place on a started League's ladder, as they see it on their Leagues page (#7).</summary>
/// <param name="Division">Their Division, 1 at the top; null when they have the Bye this Stage.</param>
/// <param name="Opponents">The other Side of their Game: one name in singles, two in doubles; empty with the Bye.</param>
public record PlayerLeaguePlace(int? Division, IReadOnlyList<string> Opponents);
