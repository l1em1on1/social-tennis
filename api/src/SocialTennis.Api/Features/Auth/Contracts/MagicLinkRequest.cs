using FluentValidation;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Membership;

namespace SocialTennis.Api.Features.Auth.Contracts;

/// <param name="Join">
/// The code of the Club join link that sent the visitor to sign in (#222), if
/// any: the emailed link carries it back to that link's page, wherever it's
/// opened. Only a code's shape is checked; whether it works is the page's to say.
/// </param>
public record MagicLinkRequest(string Email, string? Join = null)
{
    /// <summary>
    /// Shape only. Whether the address has an account is deliberately not
    /// checked — RequestMagicLink answers 202 either way (no user enumeration).
    /// </summary>
    public sealed class Validator : AbstractValidator<MagicLinkRequest>
    {
        public Validator()
        {
            RuleFor(request => request.Email).ValidEmailAddress();
            RuleFor(request => request.Join)
                .Must(JoinCodes.IsWellFormed)
                .When(request => request.Join is not null)
                .WithMessage("A join link's code is 22 letters, digits, '-' or '_'.");
        }
    }
}
