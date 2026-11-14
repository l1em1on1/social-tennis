namespace SocialTennis.Api.Features.Auth.Contracts;

/// <param name="Roles">"Manager", "Admin", both, or neither. An Admin always has "Manager" too.</param>
public record GetCurrentUserResponse(Guid UserId, string Email, IReadOnlyList<string> Roles);
