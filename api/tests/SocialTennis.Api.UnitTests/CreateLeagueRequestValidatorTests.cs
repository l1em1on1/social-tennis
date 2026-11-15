using FluentValidation.TestHelper;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.UnitTests;

/// <summary>
/// The shape rules for a new League (issue #5). Asserted by wire field name,
/// as in <see cref="MagicLinkRequestValidatorTests"/>.
/// </summary>
public class CreateLeagueRequestValidatorTests
{
    private static readonly CreateLeagueRequest.Validator Validator = new();

    private static CreateLeagueRequest ALeague(
        LeagueType type = LeagueType.SinglesMixed,
        LeagueStructure structure = LeagueStructure.Divisional,
        StageRoll? stageRoll = StageRoll.Manual,
        StageLength? stageLength = null,
        ScoreFormat? scoreFormat = null,
        string name = "Winter Singles",
        DateOnly? endDate = null) =>
        new(
            name,
            type,
            structure,
            new DateOnly(2026, 12, 1),
            endDate ?? new DateOnly(2027, 3, 31),
            stageLength ?? new StageLength(1, StageLengthUnit.Months),
            stageRoll,
            scoreFormat ?? new ScoreFormat(6, 1, false));

    [Theory]
    [InlineData(LeagueStructure.Divisional, StageRoll.Automatic)]
    [InlineData(LeagueStructure.Divisional, StageRoll.Manual)]
    [InlineData(LeagueStructure.Knockout, null)]
    public void Accepts_a_complete_League(LeagueStructure structure, StageRoll? stageRoll) =>
        Validator.TestValidate(ALeague(structure: structure, stageRoll: stageRoll))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Refuses_Foursomes_Knockout() =>
        Validator.TestValidate(ALeague(LeagueType.Foursomes, LeagueStructure.Knockout, stageRoll: null))
            .ShouldHaveValidationErrorFor("structure");

    [Fact]
    public void Accepts_Foursomes_Divisional() =>
        Validator.TestValidate(ALeague(LeagueType.Foursomes))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Refuses_a_Match_Tiebreak_on_a_one_Set_Game() =>
        Validator.TestValidate(ALeague(scoreFormat: new ScoreFormat(6, 1, true)))
            .ShouldHaveValidationErrorFor("scoreFormat.matchTiebreakDecider");

    [Theory]
    [InlineData(6, false)]
    [InlineData(9, true)]
    public void Accepts_best_of_three_with_or_without_a_Match_Tiebreak(int games, bool decider) =>
        Validator.TestValidate(ALeague(scoreFormat: new ScoreFormat(games, 3, decider)))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(0)]
    public void Refuses_games_per_Set_other_than_6_or_9(int games) =>
        Validator.TestValidate(ALeague(scoreFormat: new ScoreFormat(games, 1, false)))
            .ShouldHaveValidationErrorFor("scoreFormat.gamesPerSet");

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void Refuses_Sets_per_Game_other_than_one_or_three(int sets) =>
        Validator.TestValidate(ALeague(scoreFormat: new ScoreFormat(6, sets, false)))
            .ShouldHaveValidationErrorFor("scoreFormat.setsPerGame");

    [Fact]
    public void Requires_a_roll_mode_for_Divisional() =>
        Validator.TestValidate(ALeague(stageRoll: null))
            .ShouldHaveValidationErrorFor("stageRoll");

    [Fact]
    public void Refuses_a_roll_mode_for_Knockout() =>
        Validator.TestValidate(ALeague(structure: LeagueStructure.Knockout, stageRoll: StageRoll.Automatic))
            .ShouldHaveValidationErrorFor("stageRoll");

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Refuses_a_Stage_length_outside_1_to_12(int count) =>
        Validator.TestValidate(ALeague(stageLength: new StageLength(count, StageLengthUnit.Weeks)))
            .ShouldHaveValidationErrorFor("stageLength.count");

    [Fact]
    public void Refuses_an_end_on_or_before_the_start() =>
        Validator.TestValidate(ALeague(endDate: new DateOnly(2026, 12, 1)))
            .ShouldHaveValidationErrorFor("endDate");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Requires_a_name(string name) =>
        Validator.TestValidate(ALeague(name: name))
            .ShouldHaveValidationErrorFor("name");

    [Fact]
    public void Refuses_a_Type_that_is_not_one_of_the_seven() =>
        Validator.TestValidate(ALeague(type: 0))
            .ShouldHaveValidationErrorFor("type");
}
