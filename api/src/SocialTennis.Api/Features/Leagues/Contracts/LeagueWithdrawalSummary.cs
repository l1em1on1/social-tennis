namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A withdrawal request waiting on the Manager (#21).</summary>
/// <param name="TeamId">The Team that would leave the ladder.</param>
/// <param name="PlayerName">Whose request it is.</param>
public record LeagueWithdrawalSummary(Guid Id, Guid TeamId, string PlayerName, DateTimeOffset RequestedAt);
