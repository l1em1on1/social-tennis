using FluentValidation;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Validation;

namespace SocialTennis.Api.Features.Games.Contracts;

/// <summary>A Player in a Game says when it's played, and may note where (#9).</summary>
/// <param name="PlayerId">The Player saying so, one of the signed-in User's, in the Game.</param>
/// <param name="Date">The agreed Date; none clears it.</param>
/// <param name="StartTime">
/// When it starts on that Date, in the Club's time zone (#276), in whole minutes. Only with a Date; none leaves it
/// without one, as a Game arranged before #276.
/// </param>
/// <param name="Court">Where, for the Players' own reference ("6 - astro"): free text, optional.</param>
public record ArrangeGameRequest(Guid PlayerId, DateOnly? Date, TimeOnly? StartTime, string? Court)
{
    public sealed class Validator : AbstractValidator<ArrangeGameRequest>
    {
        public Validator()
        {
            RuleFor(request => request.PlayerId).NotEmpty().WithMessage("Say which Player is arranging it.").OverridePropertyName("playerId");
            RuleFor(request => request.StartTime)
                .Cascade(CascadeMode.Stop)
                .Must((request, time) => time is null || request.Date is not null)
                .WithMessage("Pick the day as well as the time.")
                .Must(time => time is not { } start || TimeRules.WholeMinutes(start))
                .WithMessage(TimeRules.WholeMinutesMessage)
                .OverridePropertyName("startTime");
            RuleFor(request => request.Court)
                .Must(court => court is null || court.Trim().Length <= Game.CourtMaxLength)
                .WithMessage($"Keep the court to {Game.CourtMaxLength} characters.")
                .Must(court => court is null || TextRules.HasNoControlCharacters(court))
                .WithMessage("The court can't contain control characters.")
                .OverridePropertyName("court");
        }
    }
}
