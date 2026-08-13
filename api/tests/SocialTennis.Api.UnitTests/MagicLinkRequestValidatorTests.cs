using FluentValidation.TestHelper;
using SocialTennis.Api.Features.Auth.Contracts;

namespace SocialTennis.Api.UnitTests;

/// <summary>
/// A validator is a pure function with no DbContext, which is what qualifies it
/// for this project (ADR-0010). The 400 it produces is covered end to end by the
/// integration suite; these cases cover the branches cheaply.
/// </summary>
/// <remarks>
/// Errors are asserted by wire field name rather than by property expression.
/// OverridePropertyName deliberately decouples the two, so "email" — the key a
/// client actually binds its message to — is the contract worth pinning, and
/// TestHelper's expression overload resolves to the CLR name "Email" instead.
/// </remarks>
public class MagicLinkRequestValidatorTests
{
    private const string EmailField = "email";

    private static readonly MagicLinkRequest.Validator Validator = new();

    [Theory]
    [InlineData("player@example.test")]
    [InlineData("  player@example.test  ")]
    [InlineData("PLAYER@EXAMPLE.TEST")]
    public void Accepts_a_well_formed_address(string email) =>
        Validator.TestValidate(new MagicLinkRequest(email))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public void Rejects_a_malformed_address(string email) =>
        Validator.TestValidate(new MagicLinkRequest(email))
            .ShouldHaveValidationErrorFor(EmailField);

    [Fact]
    public void Rejects_an_address_over_the_column_limit() =>
        Validator.TestValidate(new MagicLinkRequest($"{new string('a', 320)}@example.test"))
            .ShouldHaveValidationErrorFor(EmailField);
}
