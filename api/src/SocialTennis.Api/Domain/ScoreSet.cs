namespace SocialTennis.Api.Domain;

/// <summary>
/// One Set of a Score (see CONTEXT.md, Set): the games each Side won, Side 1
/// first. A Set won on a tiebreak is recorded as won by one game, 6–5 (or
/// 9–8), with both Sides' tiebreak points; no other Set has tiebreak points.
/// A Match Tiebreak (<see cref="ScoreFormat.IsMatchTiebreak"/>) holds its
/// points as its games, and counts as one game wherever games are summed.
/// </summary>
/// <remarks>
/// A Retirement's last Set may be unfinished: the games as they stood, and at
/// the tiebreak score the points of a tiebreak still being played.
/// </remarks>
public sealed record ScoreSet(int First, int Second, int? FirstTiebreak, int? SecondTiebreak);
