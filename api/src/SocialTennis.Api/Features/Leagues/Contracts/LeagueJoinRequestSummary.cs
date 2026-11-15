using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Join Request for a League, as a Manager sees it.</summary>
/// <param name="DecidedAt">When it was approved or rejected; null while pending.</param>
public record LeagueJoinRequestSummary(
    Guid PlayerId,
    string Name,
    string Email,
    int Rating,
    LeagueJoinRequestStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? DecidedAt);
