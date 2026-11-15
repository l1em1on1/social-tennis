using SocialTennis.Api.Contracts;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>The envelope for <see cref="GetClubLeagues"/> (ADR-0012).</summary>
public record GetClubLeaguesResponse(IReadOnlyList<ClubLeague> Leagues, PageInfo Page);
