namespace SocialTennis.Api.Domain;

/// <summary>
/// How every Game in a League is scored, fixed when the League is created
/// (see CONTEXT.md: Set, Match Tiebreak). The Score ticket (#38) validates
/// against it.
/// </summary>
/// <param name="GamesPerSet">6 or 9. A Set's tiebreak is played at one game short apiece (5–5, 8–8).</param>
/// <param name="SetsPerGame">1, or 3 for best of three.</param>
/// <param name="MatchTiebreakDecider">
/// Best of three only: at one Set all, a Match Tiebreak (first to 10) is played
/// instead of a third Set.
/// </param>
public record ScoreFormat(int GamesPerSet, int SetsPerGame, bool MatchTiebreakDecider)
{
    public static readonly IReadOnlyList<int> AllowedGamesPerSet = [6, 9];

    public static readonly IReadOnlyList<int> AllowedSetsPerGame = [1, 3];

    /// <summary>Whether the Set at <paramref name="index"/> (0 for the first) is played as a Match Tiebreak.</summary>
    public bool IsMatchTiebreak(int index) => MatchTiebreakDecider && SetsPerGame == 3 && index == 2;
}
