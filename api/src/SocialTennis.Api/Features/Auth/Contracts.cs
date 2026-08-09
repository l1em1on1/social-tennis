using SocialTennis.Api.Validation;

namespace SocialTennis.Api.Features.Auth;

public record MagicLinkRequest(string Email) : IValidatable
{
    // Shape only. Whether the address has an account is deliberately not
    // checked — RequestMagicLink answers 202 either way (no user enumeration).
    public Dictionary<string, string[]>? Validate() =>
        Email.Trim() is { Length: > 0 and <= 320 } email && email.Contains('@')
            ? null
            : new Dictionary<string, string[]>
            {
                ["email"] = ["A valid email address is required."],
            };
}

public record RedeemRequest(string Token);

public record SessionResponse(string Token, DateTimeOffset ExpiresAt);

public record MeResponse(Guid UserId, string Email);
