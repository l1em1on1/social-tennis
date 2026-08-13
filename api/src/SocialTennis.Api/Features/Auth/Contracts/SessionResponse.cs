namespace SocialTennis.Api.Features.Auth.Contracts;

public record SessionResponse(string Token, DateTimeOffset ExpiresAt);
