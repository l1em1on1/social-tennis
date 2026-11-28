using SocialTennis.Api.Contracts;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Games.Contracts;

/// <summary>A League Game, for its Match screen (#38).</summary>
/// <param name="ScoreFormat">What its Score is checked against.</param>
/// <param name="Division">The Division it is played in, 1 at the top.</param>
/// <param name="Sides">Side 1, then Side 2.</param>
/// <param name="Score">None until it's entered.</param>
/// <param name="NotPlayed">A Manager marked it not played (#10): it counts for nothing, and takes no Score.</param>
/// <param name="YourPlayerIds">
/// The signed-in User's Players who play in it, any of whom can enter its
/// Score. The Acting Player may be another of them, or none.
/// </param>
/// <param name="CanManage">The signed-in User runs its League, so may record, override or set aside its Score.</param>
/// <param name="Managers">Who runs its League (#220), by name; none if its Club's managers do.</param>
/// <param name="History">Every change to its Score, the latest first; none before it's entered.</param>
/// <param name="TimeZone">The Club's IANA time zone, which the history's times are shown in (ADR-0016).</param>
/// <param name="Today">Today in the Club's time zone: a Game can't have been played after it.</param>
/// <param name="Date">The Date its Players agreed (#9); none until one of them sets it.</param>
/// <param name="StartTime">When it starts on its Date, in the Club's time zone (#276); none until agreed, or for a Game arranged before #276.</param>
/// <param name="Court">Where, as its Players noted it; free text, none if they didn't.</param>
/// <param name="DateSetBy">The Player who set the Date last ("Former player" once their account is deleted); none if nobody has.</param>
/// <param name="Availability">The days and times that suit everyone in it: only for its own Players, while its Stage is open.</param>
public record GetGameResponse(
    Guid Id,
    Guid LeagueId,
    string LeagueName,
    string ClubName,
    LeagueType Type,
    ScoreFormat ScoreFormat,
    GameStage Stage,
    int Division,
    IReadOnlyList<GameSideSummary> Sides,
    GameScore? Score,
    bool NotPlayed,
    IReadOnlyList<ScoreHistoryEntry> History,
    IReadOnlyList<Guid> YourPlayerIds,
    bool CanManage,
    IReadOnlyList<LeagueManagerName> Managers,
    string TimeZone,
    DateOnly Today,
    DateOnly? Date,
    TimeOnly? StartTime,
    string? Court,
    string? DateSetBy,
    GameAvailability? Availability);
