using FluentValidation;

namespace SocialTennis.Api.Features.Clubs.Contracts;

public record InviteToClubRequest(string Email)
{
    public sealed class Validator : AbstractValidator<InviteToClubRequest>
    {
        public Validator() =>
            RuleFor(request => request.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("An email address is required.")
                .Must(email => email.Trim() is { Length: <= 320 } trimmed && trimmed.Contains('@'))
                .WithMessage("A valid email address is required.")
                .OverridePropertyName("email");
    }
}
