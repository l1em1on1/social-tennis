using FluentValidation;
using SocialTennis.Api.Validation;

namespace SocialTennis.Api.Authentication;

/// <summary>
/// The one definition of an acceptable email address and its stored form.
/// Signing in and inviting must agree exactly: an invite only lets someone in
/// if it is stored the way their User's email is.
/// </summary>
public static class EmailAddress
{
    /// <summary>The Users.Email column's width.</summary>
    public const int MaxLength = 320;

    /// <summary>The stored form: trimmed and lower-case, as in <see cref="Domain.User.Email"/>.</summary>
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// Shape only: present, short enough, has an "@". Trimming here only decides
    /// whether the value is acceptable; the handler stores <see cref="Normalize"/>.
    /// </summary>
    public static IRuleBuilderOptions<T, string> ValidEmailAddress<T>(this IRuleBuilderInitial<T, string> rule) =>
        // Stop, so the Must below can assume a non-empty string.
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("An email address is required.")
            .Must(email => email.Trim() is { Length: <= MaxLength } trimmed && trimmed.Contains('@'))
            .WithMessage("A valid email address is required.")
            .Must(TextRules.HasNoControlCharacters)
            .WithMessage("A valid email address is required.")
            // The error key is the wire's field name, which System.Text.Json
            // camel-cases; FluentValidation would otherwise emit "Email".
            .OverridePropertyName("email");
}
