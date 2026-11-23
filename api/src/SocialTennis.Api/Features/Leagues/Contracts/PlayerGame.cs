namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>One of a Player's Games this Stage (#42).</summary>
/// <param name="YourSide">The Player's Side, 1 or 2, to read the Score and its winner by.</param>
/// <param name="Partners">The Player's partner in doubles; none in singles.</param>
/// <param name="Opponents">The other Side's Players.</param>
/// <param name="Score">None until it's entered.</param>
/// <param name="NotPlayed">A Manager marked it not played (#10): it counts for nothing.</param>
public record PlayerGame(
    Guid GameId, int YourSide, IReadOnlyList<string> Partners, IReadOnlyList<string> Opponents, LadderGameScore? Score, bool NotPlayed);
