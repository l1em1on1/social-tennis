using FluentValidation;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Validation;

namespace SocialTennis.Api.Features.Clubs.Contracts;

/// <summary>The one rule for a Club's name, shared by creating and renaming.</summary>
internal static class ClubNameRules
{
    public static void ClubName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("A name is required.")
            .Must(name => name.Trim().Length <= Club.NameMaxLength)
            .WithMessage($"A name can be at most {Club.NameMaxLength} characters.")
            .Must(TextRules.HasNoControlCharacters)
            .WithMessage(TextRules.ControlCharacterMessage)
            .OverridePropertyName("name");
}
