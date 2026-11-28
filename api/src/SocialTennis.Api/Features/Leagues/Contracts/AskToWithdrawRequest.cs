using FluentValidation;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Player asks to withdraw their Team from a running League (#21).</summary>
/// <param name="PlayerId">The Player asking, one of the signed-in User's, on the League's ladder.</param>
public record AskToWithdrawRequest(Guid PlayerId)
{
    public sealed class Validator : AbstractValidator<AskToWithdrawRequest>
    {
        public Validator() =>
            RuleFor(request => request.PlayerId).NotEmpty().WithMessage("Say which Player is asking.").OverridePropertyName("playerId");
    }
}
