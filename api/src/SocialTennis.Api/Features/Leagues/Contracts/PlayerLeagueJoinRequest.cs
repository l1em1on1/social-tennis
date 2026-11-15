using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Player's own Join Request for a League, and what became of it.</summary>
public record PlayerLeagueJoinRequest(LeagueJoinRequestStatus Status, DateTimeOffset RequestedAt, DateTimeOffset? DecidedAt);
