using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>
/// One League as a Manager runs it: its format, who is in it (by join time),
/// its Join Requests (pending first, oldest first; then decided, newest first),
/// and its Teams, oldest first (#6), with their places once it has started (#7).
/// </summary>
/// <param name="DivisionSize">Teams per Division, for a Divisional League of Teams (ADR-0031); null otherwise.</param>
/// <param name="Stage">The current Stage; null until the League starts (#7).</param>
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
    IReadOnlyList<LeagueParticipantSummary> Participants,
    IReadOnlyList<LeagueJoinRequestSummary> JoinRequests,
    IReadOnlyList<LeagueTeamSummary> Teams,
    LeagueStageSummary? Stage);
