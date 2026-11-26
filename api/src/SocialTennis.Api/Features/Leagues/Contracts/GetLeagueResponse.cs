using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>
/// One League as a Manager runs it: its format, who is in it (by join time),
/// its Join Requests (pending first, oldest first; then decided, newest first),
/// and its Teams, oldest first (#6), with their places once it has started (#7).
/// </summary>
/// <param name="DivisionSize">Teams per Division, for a Divisional League of Teams (ADR-0031); null otherwise.</param>
/// <param name="StandingsMode">How its Standings rank (#214), for a Divisional League of Teams; null otherwise.</param>
/// <param name="Stage">The current Stage; null until the League starts (#7).</param>
/// <param name="Managers">Who it was assigned to (#220); none if its Club's managers run it.</param>
/// <param name="CanAssignManagers">The caller runs its Club, so says who runs it.</param>
public record GetLeagueResponse(
    Guid Id,
    Guid ClubId,
    string ClubName,
    string Name,
    LeagueType Type,
    LeagueStructure Structure,
    DateOnly StartDate,
    DateOnly EndDate,
    StageLength StageLength,
    StageRoll? StageRoll,
    ScoreFormat ScoreFormat,
    int? DivisionSize,
    StandingsMode? StandingsMode,
    IReadOnlyList<LeagueParticipantSummary> Participants,
    IReadOnlyList<LeagueJoinRequestSummary> JoinRequests,
    IReadOnlyList<LeagueTeamSummary> Teams,
    LeagueStageSummary? Stage,
    IReadOnlyList<LeagueManagerSummary> Managers,
    bool CanAssignManagers);
