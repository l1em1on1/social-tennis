using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A League as one Player sees it: one they are in, or one at their Club they could join.</summary>
/// <param name="Joined">Whether the Player is a participant.</param>
/// <param name="JoinRequest">The Player's request to join, if they made one.</param>
/// <param name="PartnerName">In a doubles League, the Player's Team partner once they have one (#6).</param>
/// <param name="PartnerRequest">A waiting request in which another Player names this one as partner (#6).</param>
public record PlayerLeague(
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
    int ParticipantCount,
    bool Joined,
    PlayerLeagueJoinRequest? JoinRequest,
    string? PartnerName,
    PlayerLeaguePartnerRequest? PartnerRequest);
