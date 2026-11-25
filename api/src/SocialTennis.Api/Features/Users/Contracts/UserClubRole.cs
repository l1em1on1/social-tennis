using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Users.Contracts;

/// <summary>A role a User holds in one Club.</summary>
public record UserClubRole(Guid ClubId, string ClubName, ClubRoleKind Role);
