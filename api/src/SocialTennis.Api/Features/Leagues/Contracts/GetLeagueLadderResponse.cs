using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>
/// A League's ladder for its current Stage, as Players and Managers see it
/// (#7): its Divisions from the top, each with its Teams and their Games
/// (ADR-0031). There is no Bye. Before the start there is no Stage, and
/// nothing on the ladder.
/// </summary>
/// <param name="Stage">The current Stage; null until the League starts.</param>
/// <param name="YourPlayerIds">
/// The signed-in User's Players in this League, on the ladder or off it: the
/// same rule that lets them see it. A User may hold several Players, and the
/// Acting Player may not be one of these.
/// </param>
public record GetLeagueLadderResponse(
    Guid Id,
    string Name,
    string ClubName,
    LeagueType Type,
    LeagueStageSummary? Stage,
    IReadOnlyList<LadderDivision> Divisions,
    IReadOnlyList<Guid> YourPlayerIds);
