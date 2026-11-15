using SocialTennis.Api.Authentication;

namespace SocialTennis.Api.UnitTests;

/// <summary>Auth:AdminEmails decides whether the last granted Admin may be demoted (ADR-0022, #109).</summary>
public class AuthOptionsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("none")]
    [InlineData(" none , ")]
    public void A_list_with_no_address_names_no_Admin(string adminEmails)
    {
        var options = new AuthOptions { AdminEmails = adminEmails };

        Assert.False(options.HasAdminEmails);
        Assert.False(options.IsAdminEmail("none"));
    }

    [Fact]
    public void Listed_addresses_are_Admins_whatever_their_case()
    {
        var options = new AuthOptions { AdminEmails = "none, Admin@Example.test" };

        Assert.True(options.HasAdminEmails);
        Assert.True(options.IsAdminEmail("admin@example.TEST"));
    }
}
