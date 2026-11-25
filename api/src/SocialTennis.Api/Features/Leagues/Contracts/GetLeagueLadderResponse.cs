using SocialTennis.Api.Contracts;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>
/// A League's ladder for its current Stage, as Players and Managers see it
/// (#7): its Divisions from the top, each with its Teams and their Games
/// (ADR-0031). There is no Bye. Before the start there is no Stage, and
/// nothing on the ladder.
/// </summary>
/// <param name="Stage">The current Stage; null until the League starts.</param>
/// <param name="HeldUp">
/// The current Stage is past its deadline and still open (ADR-0016): it waits
/// on what's in <paramref name="Awaiting"/>, or for the Manager to close it.
/// </param>
/// <param name="Awaiting">What holds up the current Stage's close (#10).</param>
/// <param name="Ended">The League's last Stage has closed (#10): the League is over, and this was its final ladder.</param>
/// <param name="YourPlayerIds">
/// The signed-in User's Players in this League, on the ladder or off it: the
/// same rule that lets them see it. A User may hold several Players, and the
/// Acting Player may not be one of these.
/// </param>
/// <param name="Managers">Who runs it (#220), by name; none if its Club's managers do.</param>
/// <param name="CanManage">The signed-in User runs it.</param>
public record GetLeagueLadderResponse(
    Guid Id,
    string Name,
    string ClubName,
    LeagueType Type,
    LeagueStageSummary? Stage,
    IReadOnlyList<LadderDivision> Divisions,
    bool HeldUp,
    LadderAwaiting Awaiting,
    bool Ended,
    IReadOnlyList<Guid> YourPlayerIds,
    IReadOnlyList<LeagueManagerName> Managers,
    bool CanManage);
