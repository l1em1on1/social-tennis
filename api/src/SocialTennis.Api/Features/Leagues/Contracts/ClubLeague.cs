using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>One of a Club's Leagues, as a Manager sees it in a list.</summary>
/// <param name="PendingRequestCount">Join Requests waiting for a Manager's decision.</param>
/// <param name="Stage">The current Stage; null until the League starts (#7).</param>
public record ClubLeague(
    Guid Id,
    string Name,
    LeagueType Type,
    LeagueStructure Structure,
    DateOnly StartDate,
    DateOnly EndDate,
    StageLength StageLength,
    StageRoll? StageRoll,
    ScoreFormat ScoreFormat,
    int ParticipantCount,
    int PendingRequestCount,
    LeagueStageSummary? Stage);
