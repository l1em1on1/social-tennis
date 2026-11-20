using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>
/// One League as a Manager runs it: its format, who is in it (by join time),
/// its Join Requests (pending first, oldest first; then decided, newest first),
/// and its Teams, oldest first (#6).
/// </summary>
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
    IReadOnlyList<LeagueParticipantSummary> Participants,
    IReadOnlyList<LeagueJoinRequestSummary> JoinRequests,
    IReadOnlyList<LeagueTeamSummary> Teams);
