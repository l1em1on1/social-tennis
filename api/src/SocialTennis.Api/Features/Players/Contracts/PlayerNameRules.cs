using FluentValidation;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Players.Contracts;

/// <summary>The one rule for a Player's Name, shared by creating and renaming.</summary>
internal static class PlayerNameRules
{
    public static void PlayerName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("A name is required.")
            .Must(name => name.Trim().Length <= Player.NameMaxLength)
            .WithMessage($"A name can be at most {Player.NameMaxLength} characters.")
            .OverridePropertyName("name");
}
