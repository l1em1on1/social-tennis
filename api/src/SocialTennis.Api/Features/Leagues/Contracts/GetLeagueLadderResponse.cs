using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>
/// A League's ladder for its current Stage, as Players and Managers see it
/// (#7): its Divisions from the top, each with its Game, and the Bye. Before
/// the start there is no Stage, and nothing on the ladder.
/// </summary>
/// <param name="Stage">The current Stage; null until the League starts.</param>
/// <param name="Bye">The Competitor sitting out this Stage, when the number is odd.</param>
public record GetLeagueLadderResponse(
    Guid Id,
    string Name,
    string ClubName,
    LeagueType Type,
    LeagueStageSummary? Stage,
    IReadOnlyList<LadderDivision> Divisions,
    LadderCompetitor? Bye);
