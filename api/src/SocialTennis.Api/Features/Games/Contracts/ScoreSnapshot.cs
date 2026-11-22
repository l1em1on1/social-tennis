using SocialTennis.Api.Contracts;

namespace SocialTennis.Api.Features.Games.Contracts;

/// <summary>What a Score said before or after one change.</summary>
/// <param name="Winner">The Side that won, 1 or 2.</param>
public record ScoreSnapshot(DateOnly PlayedOn, IReadOnlyList<SetScore> Sets, int Winner, bool Retired);
