using FluentValidation;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Games.Contracts;

/// <summary>A Game's Score as corrected by a Player who played it, while its Stage is open (#38).</summary>
/// <param name="PlayerId">The signed-in User's Player correcting it: one who played the Game.</param>
/// <param name="Version">The Score's version the correction was made from (ADR-0018).</param>
public record CorrectScoreRequest(
    Guid PlayerId, DateOnly PlayedOn, IReadOnlyList<ScoreSet> Sets, int? RetirementWinner, uint Version) : IScoreEntry
{
    public sealed class Validator : AbstractValidator<CorrectScoreRequest>
    {
        public Validator()
        {
            RuleFor(request => request.PlayerId)
                .NotEmpty()
                .WithMessage("Say which Player is correcting the score.")
                .OverridePropertyName("playerId");
            Include(new IScoreEntry.Validator());
        }
    }
}
