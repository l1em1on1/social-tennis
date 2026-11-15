using FluentValidation;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Validation;

namespace SocialTennis.Api.Features.Players.Contracts;

/// <summary>The rules for a Player's own fields, shared by every request that carries them.</summary>
public static class PlayerRules
{
    public static void PlayerName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("A name is required.")
            .Must(name => name.Trim().Length <= Player.NameMaxLength)
            .WithMessage($"A name can be at most {Player.NameMaxLength} characters.")
            .Must(TextRules.HasNoControlCharacters)
            .WithMessage(TextRules.ControlCharacterMessage)
            .OverridePropertyName("name");

    /// <summary>
    /// Unset is fine. An unknown name ("Other") fails to bind, but a number
    /// binds to any value of the enum's type, so it is checked here (#109).
    /// </summary>
    public static void PlayerGender<T>(this IRuleBuilderInitial<T, PlayerGender?> rule) =>
        rule.IsInEnum()
            .WithMessage("Choose a gender the form offers.")
            .OverridePropertyName("gender");

    /// <summary>Unset is fine.</summary>
    public static void PlayerLevel<T>(this IRuleBuilderInitial<T, int?> rule) =>
        rule.Must(level => level is null or (>= Player.MinLevel and <= Player.MaxLevel))
            .WithMessage($"A level is between {Player.MinLevel} and {Player.MaxLevel}.")
            .OverridePropertyName("level");
}
