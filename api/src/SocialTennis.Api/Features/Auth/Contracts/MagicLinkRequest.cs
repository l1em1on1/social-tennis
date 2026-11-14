using FluentValidation;
using SocialTennis.Api.Authentication;

namespace SocialTennis.Api.Features.Auth.Contracts;

public record MagicLinkRequest(string Email)
{
    /// <summary>
    /// Shape only. Whether the address has an account is deliberately not
    /// checked — RequestMagicLink answers 202 either way (no user enumeration).
    /// </summary>
    public sealed class Validator : AbstractValidator<MagicLinkRequest>
    {
        public Validator() => RuleFor(request => request.Email).ValidEmailAddress();
    }
}
