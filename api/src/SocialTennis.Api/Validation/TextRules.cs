namespace SocialTennis.Api.Validation;

/// <summary>Rules every free-text field shares.</summary>
public static class TextRules
{
    /// <summary>
    /// No control characters (NUL, newlines, tabs and the rest). Postgres
    /// refuses a NUL in text, which would surface as a 500 (#109), and none of
    /// them belongs in a name or an email address.
    /// </summary>
    public static bool HasNoControlCharacters(string value) => !value.Any(char.IsControl);

    public const string ControlCharacterMessage = "Use letters, numbers, spaces and punctuation only.";
}
