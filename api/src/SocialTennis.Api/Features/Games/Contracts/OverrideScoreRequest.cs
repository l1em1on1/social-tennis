using FluentValidation;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Games.Contracts;

/// <summary>A Game's Score as a Manager overrides it, open Stage or closed (#38).</summary>
/// <param name="Version">The Score's version the override was made from (ADR-0018).</param>
public record OverrideScoreRequest(DateOnly PlayedOn, IReadOnlyList<ScoreSet> Sets, int? RetirementWinner, uint Version) : IScoreEntry
{
    public sealed class Validator : AbstractValidator<OverrideScoreRequest>
    {
        public Validator() => Include(new IScoreEntry.Validator());
    }
}
