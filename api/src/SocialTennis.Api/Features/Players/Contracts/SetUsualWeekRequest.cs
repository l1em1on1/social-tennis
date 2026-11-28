using FluentValidation;

namespace SocialTennis.Api.Features.Players.Contracts;

/// <summary>A Player's whole usual week (#276): it replaces what they said before.</summary>
/// <param name="Days">The weekdays they're usually free, each once; a weekday not listed is one they usually aren't. None clears it.</param>
public record SetUsualWeekRequest(IReadOnlyList<UsualWeekDay> Days)
{
    public sealed class Validator : AbstractValidator<SetUsualWeekRequest>
    {
        public Validator() =>
            RuleFor(request => request.Days)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage("Say which days, even if none.")
                .Must(days => days.All(day => day is not null))
                .WithMessage("Say which days, even if none.")
                .Must(days => days.Select(day => day.Weekday).Distinct().Count() == days.Count)
                .WithMessage("Give each day of the week once.")
                .Must(days => days.All(day => day.Problem() is null))
                .WithMessage(request => request.Days.Select(day => day.Problem()).First(problem => problem is not null)!)
                .OverridePropertyName("days");
    }
}
