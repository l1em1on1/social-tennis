using FluentValidation;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Validation;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>The one rule for a League's name, shared by creating and renaming (#153).</summary>
internal static class LeagueNameRules
{
    public static void LeagueName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("A name is required.")
            .Must(name => name.Trim().Length <= League.NameMaxLength)
            .WithMessage($"A name can be at most {League.NameMaxLength} characters.")
            .Must(TextRules.HasNoControlCharacters)
            .WithMessage(TextRules.ControlCharacterMessage)
            .OverridePropertyName("name");
}
