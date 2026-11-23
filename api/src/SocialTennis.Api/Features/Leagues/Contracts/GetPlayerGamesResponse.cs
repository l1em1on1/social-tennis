using SocialTennis.Api.Contracts;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Player's Games this Stage, by League (#42): what Home shows them. An envelope (ADR-0012).</summary>
/// <param name="Leagues">Each League they're in whose current Stage is open, the soonest deadline first.</param>
public record GetPlayerGamesResponse(IReadOnlyList<PlayerGamesLeague> Leagues, PageInfo Page);
