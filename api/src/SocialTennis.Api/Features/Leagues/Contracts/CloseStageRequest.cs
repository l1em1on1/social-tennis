using FluentValidation;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>The Manager confirms closing the current Stage (#10), as the close screen showed it.</summary>
/// <param name="Divisions">
/// Every Division, top first, as Team ids in its final order: its Standings,
/// with the Manager's order where a tie decides who moves.
/// </param>
/// <param name="Joining">Waiting Teams the Manager adds, in the order they join the bottom.</param>
/// <param name="NextStartDate">The next Stage's first day; none when this Stage is the League's last.</param>
/// <param name="NextEndDate">Its last day, the deadline.</param>
public record CloseStageRequest(
    IReadOnlyList<IReadOnlyList<Guid>> Divisions, IReadOnlyList<Guid> Joining, DateOnly? NextStartDate, DateOnly? NextEndDate)
{
    public sealed class Validator : AbstractValidator<CloseStageRequest>
    {
        public Validator()
        {
            RuleFor(request => request.Divisions)
                .NotEmpty()
                .WithMessage("Every Division, in its final order.")
                .OverridePropertyName("divisions");
            RuleFor(request => request.Joining)
                .NotNull()
                .WithMessage("The Teams joining, or none.")
                .OverridePropertyName("joining");
            RuleFor(request => request.NextEndDate)
                .Must((request, end) => request.NextStartDate is null || end is null || end >= request.NextStartDate)
                .WithMessage("The next Stage ends on or after the day it starts.")
                .Must((request, end) => (request.NextStartDate is null) == (end is null))
                .WithMessage("Give the next Stage both its dates.")
                .OverridePropertyName("nextEndDate");
        }
    }
}
