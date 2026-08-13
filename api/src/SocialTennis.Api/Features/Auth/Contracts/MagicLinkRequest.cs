using FluentValidation;

namespace SocialTennis.Api.Features.Auth.Contracts;

public record MagicLinkRequest(string Email)
{
    /// <summary>
    /// Shape only. Whether the address has an account is deliberately not
    /// checked — RequestMagicLink answers 202 either way (no user enumeration).
    /// </summary>
    public sealed class Validator : AbstractValidator<MagicLinkRequest>
    {
        public Validator() =>
            RuleFor(request => request.Email)
                // Stop, so the Must below can assume a non-empty string.
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("An email address is required.")
                // 320 is the Email column's width; trimming here only decides
                // whether the value is acceptable — HandleAsync does the actual
                // normalisation.
                .Must(email => email.Trim() is { Length: <= 320 } trimmed && trimmed.Contains('@'))
                .WithMessage("A valid email address is required.")
                // The error key is the wire's field name, which System.Text.Json
                // camel-cases; FluentValidation would otherwise emit "Email".
                .OverridePropertyName("email");
    }
}
