using FluentValidation;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>The days a Player is free in their League's current Stage (#9), all of them: it replaces what they said before.</summary>
/// <param name="Dates">Whole days, inside the Stage. None clears them.</param>
public record SetAvailabilityRequest(IReadOnlyList<DateOnly> Dates)
{
    public sealed class Validator : AbstractValidator<SetAvailabilityRequest>
    {
        public Validator() =>
            RuleFor(request => request.Dates)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage("Say which days, even if none.")
                .Must(dates => dates.Count <= 400)
                .WithMessage("That's more days than a Stage has.")
                .OverridePropertyName("dates");
    }
}
