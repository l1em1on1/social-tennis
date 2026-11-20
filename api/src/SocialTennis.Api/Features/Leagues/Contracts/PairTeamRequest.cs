using FluentValidation;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>Two unpaired Players of a doubles League, for the Manager to make a Team of (#6).</summary>
public record PairTeamRequest(Guid FirstPlayerId, Guid SecondPlayerId)
{
    public sealed class Validator : AbstractValidator<PairTeamRequest>
    {
        public Validator() =>
            RuleFor(request => request.SecondPlayerId)
                .NotEqual(request => request.FirstPlayerId)
                .WithMessage("A Team is two different Players.");
    }
}
