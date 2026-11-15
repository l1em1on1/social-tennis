using FluentValidation;
using SocialTennis.Api.Domain;

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
            .OverridePropertyName("name");

    /// <summary>Unset is fine. An unknown Gender never gets this far: it fails to bind.</summary>
    public static void PlayerLevel<T>(this IRuleBuilderInitial<T, int?> rule) =>
        rule.Must(level => level is null or (>= Player.MinLevel and <= Player.MaxLevel))
            .WithMessage($"A level is between {Player.MinLevel} and {Player.MaxLevel}.")
            .OverridePropertyName("level");
}
