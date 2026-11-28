using FluentValidation;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>
/// The dates a Player sets differently from their usual week in their League's current Stage (#9, #276), all of
/// them: it replaces what they set before. Every date not listed follows the usual week again.
/// </summary>
/// <param name="Days">Dates inside the Stage, each once. None: every date follows the usual week.</param>
public record SetAvailabilityRequest(IReadOnlyList<AvailabilityDaySetting> Days)
{
    public sealed class Validator : AbstractValidator<SetAvailabilityRequest>
    {
        public Validator() =>
            RuleFor(request => request.Days)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage("Say which days, even if none.")
                .Must(days => days.All(day => day is not null))
                .WithMessage("Say which days, even if none.")
                .Must(days => days.Count <= 400)
                .WithMessage("That's more days than a Stage has.")
                .Must(days => days.Select(day => day.Date).Distinct().Count() == days.Count)
                .WithMessage("Set each day once.")
                .Must(days => days.All(day => day.Problem() is null))
                .WithMessage(request => request.Days.Select(day => day.Problem()).First(problem => problem is not null)!)
                .OverridePropertyName("days");
    }
}
