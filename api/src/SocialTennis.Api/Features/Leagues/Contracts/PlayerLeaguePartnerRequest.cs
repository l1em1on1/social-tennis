namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>
/// A waiting Join Request in which another Player names this one as their
/// partner (#6). The partner can take it back. Once it is decided they either
/// play together or, if it was rejected, are free to ask themselves.
/// </summary>
/// <param name="PlayerId">The Player who asked, which addresses the request.</param>
/// <param name="Name">That Player's name.</param>
public record PlayerLeaguePartnerRequest(Guid PlayerId, string Name, DateTimeOffset RequestedAt);
