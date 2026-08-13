namespace SocialTennis.Api.Features.Auth.Contracts;

public record GetCurrentUserResponse(Guid UserId, string Email);
