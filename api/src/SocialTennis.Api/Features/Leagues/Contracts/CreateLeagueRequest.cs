using FluentValidation;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>Everything a League fixes when a Manager creates it (issue #5).</summary>
/// <param name="StartDate">The first Stage's first day, a calendar date in the Club's time zone (ADR-0016).</param>
/// <param name="EndDate">The League's last day. Must be after <paramref name="StartDate"/>.</param>
/// <param name="StageRoll">
/// Required for a Divisional League, and refused for a Knockout one, which has
/// no Relegation to roll into.
/// </param>
/// <param name="DivisionSize">
/// Teams per Division for a Divisional League of the Team-based Types, from
/// 2 to 8; 4 when left out (ADR-0031). Refused for Knockout, which has no
/// Divisions, and Foursomes, whose Divisions are always 4 Players.
/// </param>
/// <remarks>
/// A start date in the past is allowed: a club already part-way through a
/// League's month can put it in the app and carry on (phase 1, docs/phase-1-score-collection.md).
/// </remarks>
public record CreateLeagueRequest(
    string Name,
    LeagueType Type,
    LeagueStructure Structure,
    DateOnly StartDate,
    DateOnly EndDate,
    StageLength StageLength,
    StageRoll? StageRoll,
    ScoreFormat ScoreFormat,
    int? DivisionSize = null)
{
    public sealed class Validator : AbstractValidator<CreateLeagueRequest>
    {
        public Validator()
        {
            RuleFor(request => request.Name).LeagueName();

            RuleFor(request => request.Type).IsInEnum()
                .WithMessage("Choose a League Type.")
                .OverridePropertyName("type");

            RuleFor(request => request.Structure).Cascade(CascadeMode.Stop)
                .IsInEnum()
                .WithMessage("Choose Divisional or Knockout.")
                .Must((request, structure) => League.IsValidCombination(request.Type, structure))
                .WithMessage("Foursomes is played in Divisions of 4 only, so a Foursomes League can't be Knockout.")
                .OverridePropertyName("structure");

            RuleFor(request => request.EndDate)
                .Must((request, end) => end > request.StartDate)
                .WithMessage("The League has to end after it starts.")
                .OverridePropertyName("endDate");

            RuleFor(request => request.StageLength).NotNull()
                .WithMessage("A Stage length is required.")
                .OverridePropertyName("stageLength");
            When(request => request.StageLength is not null, () =>
            {
                RuleFor(request => request.StageLength.Count)
                    .InclusiveBetween(StageLength.MinCount, StageLength.MaxCount)
                    .WithMessage($"A Stage lasts from {StageLength.MinCount} to {StageLength.MaxCount} weeks or months.")
                    .OverridePropertyName("stageLength.count");
                RuleFor(request => request.StageLength.Unit).IsInEnum()
                    .WithMessage("A Stage lasts a number of weeks or months.")
                    .OverridePropertyName("stageLength.unit");
            });

            RuleFor(request => request.StageRoll)
                .NotNull()
                .When(request => request.Structure == LeagueStructure.Divisional)
                .WithMessage("Choose whether each Stage closes automatically or when you close it.")
                .OverridePropertyName("stageRoll");
            RuleFor(request => request.StageRoll)
                .IsInEnum()
                .When(request => request.StageRoll is not null)
                .WithMessage("Stages roll automatically or manually.")
                .OverridePropertyName("stageRoll");
            RuleFor(request => request.StageRoll)
                .Null()
                .When(request => request.Structure == LeagueStructure.Knockout)
                .WithMessage("A Knockout League has no Relegation, so its Stages don't roll: leave this out.")
                .OverridePropertyName("stageRoll");

            RuleFor(request => request.DivisionSize)
                .InclusiveBetween(League.MinDivisionSize, League.MaxDivisionSize)
                .When(request => request.DivisionSize is not null)
                .WithMessage($"A Division holds from {League.MinDivisionSize} to {League.MaxDivisionSize} Teams.")
                .OverridePropertyName("divisionSize");
            RuleFor(request => request.DivisionSize)
                .Null()
                .When(request => request.Structure == LeagueStructure.Knockout)
                .WithMessage("A Knockout League has no Divisions: leave this out.")
                .OverridePropertyName("divisionSize");
            RuleFor(request => request.DivisionSize)
                .Null()
                .When(request => request.Type == LeagueType.Foursomes)
                .WithMessage("A Foursomes Division is always 4 Players: leave this out.")
                .OverridePropertyName("divisionSize");

            RuleFor(request => request.ScoreFormat).NotNull()
                .WithMessage("A Score format is required.")
                .OverridePropertyName("scoreFormat");
            When(request => request.ScoreFormat is not null, () =>
            {
                RuleFor(request => request.ScoreFormat.GamesPerSet)
                    .Must(games => ScoreFormat.AllowedGamesPerSet.Contains(games))
                    .WithMessage("A Set is played to 6 or 9 games.")
                    .OverridePropertyName("scoreFormat.gamesPerSet");
                RuleFor(request => request.ScoreFormat.SetsPerGame)
                    .Must(sets => ScoreFormat.AllowedSetsPerGame.Contains(sets))
                    .WithMessage("A Game is one Set, or best of three.")
                    .OverridePropertyName("scoreFormat.setsPerGame");
                RuleFor(request => request.ScoreFormat.MatchTiebreakDecider)
                    .Must((request, decider) => !decider || request.ScoreFormat.SetsPerGame == 3)
                    .WithMessage("A Match Tiebreak can only decide a best-of-three Game. A one-Set Game has no decider.")
                    .OverridePropertyName("scoreFormat.matchTiebreakDecider");
            });
        }
    }
}
