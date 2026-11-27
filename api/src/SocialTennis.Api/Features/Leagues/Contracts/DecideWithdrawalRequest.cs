using FluentValidation;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>The Manager accepts or declines a withdrawal request (#21).</summary>
public record DecideWithdrawalRequest(bool Accept)
{
    public sealed class Validator : AbstractValidator<DecideWithdrawalRequest>
    {
        public Validator()
        {
        }
    }
}
