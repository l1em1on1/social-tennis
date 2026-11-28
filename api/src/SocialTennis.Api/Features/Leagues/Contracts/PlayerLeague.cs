using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A League as one Player sees it: one they are in, or one at their Club they could join.</summary>
/// <param name="DivisionSize">Teams per Division, for a Divisional League of Teams (ADR-0031); null otherwise.</param>
/// <param name="StandingsMode">How its Standings rank (#214), for a Divisional League of Teams; null otherwise.</param>
/// <param name="Joined">Whether the Player is a participant.</param>
/// <param name="JoinRequest">The Player's request to join, if they made one.</param>
/// <param name="PartnerName">In a doubles League, the Player's Team partner once they have one (#6).</param>
/// <param name="PartnerRequest">A waiting request in which another Player names this one as partner (#6).</param>
/// <param name="Stage">The current Stage; null until the League starts (#7).</param>
/// <param name="Place">The Player's place in that Stage; null if they aren't on its ladder (#7).</param>
/// <param name="Withdrawal">The withdrawal request that matters to the Player's Team (#21); none if there's been none.</param>
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
    int? DivisionSize,
    StandingsMode? StandingsMode,
    int ParticipantCount,
    bool Joined,
    PlayerLeagueJoinRequest? JoinRequest,
    string? PartnerName,
    PlayerLeaguePartnerRequest? PartnerRequest,
    LeagueStageSummary? Stage,
    PlayerLeaguePlace? Place,
    PlayerLeagueWithdrawal? Withdrawal = null);
