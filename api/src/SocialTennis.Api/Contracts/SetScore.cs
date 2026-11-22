using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Contracts;

/// <summary>
/// One Set of a Game's Score as the app shows it (#38): Side 1's games first,
/// both Sides' tiebreak points for a Set won on a tiebreak, and whether it was
/// a Match Tiebreak, whose games are its points. The Match screen and the
/// ladder show it alike.
/// </summary>
public record SetScore(int First, int Second, int? FirstTiebreak, int? SecondTiebreak, bool MatchTiebreak)
{
    public static IReadOnlyList<SetScore> Of(ScoreFormat format, IReadOnlyList<ScoreSet> sets) =>
        [.. sets.Select((set, index) => new SetScore(set.First, set.Second, set.FirstTiebreak, set.SecondTiebreak, format.IsMatchTiebreak(index)))];
}
