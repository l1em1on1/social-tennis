using SocialTennis.Api.Contracts;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>
/// The envelope for <see cref="GetPlayerLeagues"/> (ADR-0012): the Leagues the
/// Player is in and those at their Clubs they could join, in one list split by
/// <see cref="PlayerLeague.Joined"/>.
/// </summary>
public record GetPlayerLeaguesResponse(IReadOnlyList<PlayerLeague> Leagues, PageInfo Page);
