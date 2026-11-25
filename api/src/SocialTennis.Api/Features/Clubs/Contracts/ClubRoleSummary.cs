using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Clubs.Contracts;

/// <summary>A role someone holds in a Club (#110, #220), for the managers who give and take it.</summary>
/// <param name="Email">Theirs: a manager needn't hold a Player.</param>
public record ClubRoleSummary(Guid UserId, string Email, ClubRoleKind Role);
