using FluentValidation;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Manager moves a Team on the ladder to another Division mid-Stage (#10).</summary>
/// <param name="ToDivision">The Division it moves to, 1 at the top; it starts at the bottom.</param>
public record MoveTeamRequest(Guid TeamId, int ToDivision)
{
    public sealed class Validator : AbstractValidator<MoveTeamRequest>
    {
        public Validator()
        {
            RuleFor(request => request.TeamId)
                .NotEmpty()
                .WithMessage("Say which Team moves.")
                .OverridePropertyName("teamId");
            RuleFor(request => request.ToDivision)
                .GreaterThanOrEqualTo(1)
                .WithMessage("Divisions are numbered from 1, at the top.")
                .OverridePropertyName("toDivision");
        }
    }
}
