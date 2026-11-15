using SocialTennis.Api.Contracts;

namespace SocialTennis.Api.Features.Users.Contracts;

/// <summary>The envelope for <see cref="GetUsers"/> (ADR-0012).</summary>
public record GetUsersResponse(IReadOnlyList<UserSummary> Users, PageInfo Page);
