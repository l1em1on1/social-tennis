using FluentValidation;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Games.Contracts;

/// <summary>A Score for a Game, entered by a Player who played it (#38).</summary>
/// <param name="PlayerId">The signed-in User's Player entering it: one who played the Game.</param>
public record SubmitScoreRequest(Guid PlayerId, DateOnly PlayedOn, IReadOnlyList<ScoreSet> Sets, int? RetirementWinner) : IScoreEntry
{
    public sealed class Validator : AbstractValidator<SubmitScoreRequest>
    {
        public Validator()
        {
            RuleFor(request => request.PlayerId)
                .NotEmpty()
                .WithMessage("Say which Player is entering the score.")
                .OverridePropertyName("playerId");
            Include(new IScoreEntry.Validator());
        }
    }
}
