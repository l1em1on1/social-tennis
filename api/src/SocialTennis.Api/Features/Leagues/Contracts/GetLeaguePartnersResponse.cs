using SocialTennis.Api.Contracts;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>The envelope for <see cref="GetLeaguePartners"/> (ADR-0012).</summary>
public record GetLeaguePartnersResponse(IReadOnlyList<LeaguePartner> Partners, PageInfo Page);
