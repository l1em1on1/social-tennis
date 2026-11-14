using SocialTennis.Api.Contracts;

namespace SocialTennis.Api.Features.Players.Contracts;

/// <summary>The envelope for <see cref="GetPlayers"/> (ADR-0012).</summary>
public record GetPlayersResponse(IReadOnlyList<PlayerSummary> Players, PageInfo Page);
