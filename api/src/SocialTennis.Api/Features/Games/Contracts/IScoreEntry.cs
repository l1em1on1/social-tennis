using FluentValidation;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Games.Contracts;

/// <summary>What entering, correcting and overriding a Score send alike (#38).</summary>
public interface IScoreEntry
{
    /// <summary>A calendar date in the Club's time zone, from the Stage's start to today (ADR-0016).</summary>
    DateOnly PlayedOn { get; }

    /// <summary>
    /// In the order played, Side 1's games first. A Set won on a tiebreak is
    /// 6–5 (or 9–8) with both Sides' points; a Match Tiebreak's games are its points.
    /// </summary>
    IReadOnlyList<ScoreSet> Sets { get; }

    /// <summary>
    /// For a Game that ended early, the Side declared the winner (1 or 2), with
    /// the Sets as they stood. None for a Game played to the end.
    /// </summary>
    int? RetirementWinner { get; }

    /// <summary>The highest games or points a Set can hold: a long Match Tiebreak stays well under it.</summary>
    public const int MaxInSet = 99;

    /// <summary>Shape only: what the League's format allows is <see cref="ScoreRules"/>, in the handler.</summary>
    public sealed class Validator : AbstractValidator<IScoreEntry>
    {
        public Validator()
        {
            RuleFor(entry => entry.Sets)
                .NotNull()
                .WithMessage("Enter the score of each set.")
                .Must(sets => sets is null || sets.All(InRange))
                .WithMessage($"Games and points are whole numbers from 0 to {MaxInSet}.")
                .OverridePropertyName("sets");
            RuleFor(entry => entry.RetirementWinner)
                .Must(side => side is null or 1 or 2)
                .WithMessage("A retirement's winner is Side 1 or Side 2.")
                .OverridePropertyName("retirementWinner");
        }

        private static bool InRange(ScoreSet? set) =>
            set is not null
            && new[] { set.First, set.Second, set.FirstTiebreak ?? 0, set.SecondTiebreak ?? 0 }.All(value => value is >= 0 and <= MaxInSet);
    }
}
