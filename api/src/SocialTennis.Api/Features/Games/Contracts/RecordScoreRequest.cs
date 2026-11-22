using FluentValidation;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Games.Contracts;

/// <summary>A Score a Manager records for a Game nobody has scored (#10).</summary>
public record RecordScoreRequest(DateOnly PlayedOn, IReadOnlyList<ScoreSet> Sets, int? RetirementWinner) : IScoreEntry
{
    public sealed class Validator : AbstractValidator<RecordScoreRequest>
    {
        public Validator() => Include(new IScoreEntry.Validator());
    }
}
