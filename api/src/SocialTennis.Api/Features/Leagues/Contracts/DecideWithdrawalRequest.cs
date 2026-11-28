using FluentValidation;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>The Manager accepts or declines a withdrawal request (#21).</summary>
/// <param name="Accept">True accepts, false declines. Required: a decision left out is a 400, never a decline.</param>
public record DecideWithdrawalRequest(bool? Accept)
{
    public sealed class Validator : AbstractValidator<DecideWithdrawalRequest>
    {
        public Validator() =>
            RuleFor(request => request.Accept).NotNull().WithMessage("Say whether to accept or decline.").OverridePropertyName("accept");
    }
}
