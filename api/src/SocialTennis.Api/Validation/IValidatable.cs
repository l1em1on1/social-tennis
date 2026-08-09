namespace SocialTennis.Api.Validation;

/// <summary>
/// A request contract that can check its own shape. Implemented on the request
/// record in a feature's Contracts.cs, so the rules sit next to the shape they
/// constrain and stay readable C# rather than attribute metadata.
/// </summary>
/// <remarks>
/// Shape only — anything needing a <c>DbContext</c>, configuration, or the
/// current user is a business rule and belongs in the endpoint's HandleAsync.
/// Normalisation (trimming, lower-casing) is not validation and belongs there
/// too; Validate must not mutate.
/// </remarks>
public interface IValidatable
{
    /// <summary>
    /// Field name to error messages, in the shape ValidationProblem expects, or
    /// null when the request is well-formed.
    /// </summary>
    Dictionary<string, string[]>? Validate();
}
