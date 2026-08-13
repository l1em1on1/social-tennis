namespace SocialTennis.Api.Features.Auth.Contracts;

public record RedeemMagicLinkResponse(string Token, DateTimeOffset ExpiresAt);
