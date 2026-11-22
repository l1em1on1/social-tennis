using SocialTennis.Api.Contracts;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A ladder Game's result (#38): its Match screen has the rest.</summary>
/// <param name="Winner">The Side that won, 1 or 2.</param>
/// <param name="Retired">The Game ended early (a Retirement): still a played result.</param>
public record LadderGameScore(int Winner, bool Retired, IReadOnlyList<SetScore> Sets);
