namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>One of a Player's Games this Stage (#42).</summary>
/// <param name="YourSide">The Player's Side, 1 or 2, to read the Score and its winner by.</param>
/// <param name="Partners">The Player's partner in doubles; none in singles.</param>
/// <param name="Opponents">The other Side's Players.</param>
/// <param name="Score">None until it's entered.</param>
/// <param name="NotPlayed">A Manager marked it not played (#10): it counts for nothing.</param>
/// <param name="Date">The Date its Players agreed (#9); none until one of them sets it.</param>
/// <param name="StartTime">When it starts on its Date, in the Club's time zone (#276); none until agreed, or for a Game arranged before #276.</param>
/// <param name="Court">Where, as its Players noted it; none if they didn't.</param>
public record PlayerGame(
    Guid GameId,
    int YourSide,
    IReadOnlyList<string> Partners,
    IReadOnlyList<string> Opponents,
    LadderGameScore? Score,
    bool NotPlayed,
    DateOnly? Date,
    TimeOnly? StartTime,
    string? Court);
