using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Games.Contracts;

/// <summary>A League Game, for its Match screen (#38).</summary>
/// <param name="ScoreFormat">What its Score is checked against.</param>
/// <param name="Division">The Division it is played in, 1 at the top.</param>
/// <param name="Sides">Side 1, then Side 2.</param>
/// <param name="Score">None until it's entered.</param>
/// <param name="YourPlayerIds">
/// The signed-in User's Players who play in it, any of whom can enter its
/// Score. The Acting Player may be another of them, or none.
/// </param>
/// <param name="Today">Today in the Club's time zone: a Game can't have been played after it.</param>
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
    IReadOnlyList<Guid> YourPlayerIds,
    DateOnly Today);
