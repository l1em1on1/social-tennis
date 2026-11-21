using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>The partner a doubles Join Request names, as a Manager sees them (#6).</summary>
public record LeagueJoinRequestPartner(Guid PlayerId, string Name, string Email, int Rating, PlayerGender? Gender);
