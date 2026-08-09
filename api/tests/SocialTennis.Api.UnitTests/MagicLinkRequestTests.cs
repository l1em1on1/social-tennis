using SocialTennis.Api.Features.Auth;

namespace SocialTennis.Api.UnitTests;

/// <summary>
/// Validate() is a pure function with no DbContext, which is what qualifies it
/// for this project (ADR-0010). The 400 it produces is covered end to end by the
/// integration suite; these cases cover the branches cheaply.
/// </summary>
public class MagicLinkRequestTests
{
    [Theory]
    [InlineData("player@example.test")]
    [InlineData("  player@example.test  ")]
    [InlineData("PLAYER@EXAMPLE.TEST")]
    public void Accepts_a_well_formed_address(string email) =>
        Assert.Null(new MagicLinkRequest(email).Validate());

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public void Rejects_a_malformed_address(string email) =>
        Assert.Equal(["email"], new MagicLinkRequest(email).Validate()?.Keys);

    [Fact]
    public void Rejects_an_address_over_the_column_limit() =>
        Assert.NotNull(new MagicLinkRequest($"{new string('a', 320)}@example.test").Validate());
}
