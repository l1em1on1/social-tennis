using SocialTennis.Api.Contracts;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>The envelope for <see cref="GetAddablePlayers"/> (ADR-0012).</summary>
public record GetAddablePlayersResponse(IReadOnlyList<AddablePlayer> Players, PageInfo Page);
