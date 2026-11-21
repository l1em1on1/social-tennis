using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Player's own Join Request for a League, and what became of it.</summary>
/// <param name="PartnerName">The partner it names, in a doubles League (#6); null if they asked alone.</param>
public record PlayerLeagueJoinRequest(
    LeagueJoinRequestStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? DecidedAt,
    string? PartnerName);
