using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Join Request for a League, as a Manager sees it.</summary>
/// <param name="Gender">The asker's, for the mixed doubles warning (#6).</param>
/// <param name="Partner">The partner it names, in a doubles League (#6); null if they asked alone.</param>
/// <param name="DecidedAt">When it was approved or rejected; null while pending.</param>
public record LeagueJoinRequestSummary(
    Guid PlayerId,
    string Name,
    string Email,
    int Rating,
    PlayerGender? Gender,
    LeagueJoinRequestPartner? Partner,
    LeagueJoinRequestStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? DecidedAt);
