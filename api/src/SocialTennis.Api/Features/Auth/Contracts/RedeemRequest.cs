namespace SocialTennis.Api.Features.Auth.Contracts;

/// <summary>
/// Deliberately unvalidated: a token is opaque, so an empty one is not usefully
/// different from a wrong one. RedeemMagicLink answers 401 to both rather than
/// distinguishing them with a 400 (ADR-0011).
/// </summary>
public record RedeemRequest(string Token);
