using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>
/// The withdrawal request that matters to the Player's Team (#21): one waiting, else the accepted
/// one (they've withdrawn), else the latest declined, so they can see what the Manager decided. Accepted with no request
/// time of the Player's own means the Manager withdrew them directly.
/// </summary>
public record PlayerLeagueWithdrawal(WithdrawalStatus Status, DateTimeOffset RequestedAt, DateTimeOffset? DecidedAt);
