using SocialTennis.Api.Authentication;

namespace SocialTennis.Api.UnitTests;

/// <summary>The key a sign-in request's client is limited by (#108).</summary>
public class ClientAddressTests
{
    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData(" 203.0.113.7 ", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    public void An_IPv4_address_is_its_own_key(string header, string key) =>
        Assert.Equal(key, ClientAddress.Normalize(header));

    [Fact]
    public void Addresses_in_one_IPv6_64_share_a_key()
    {
        var first = ClientAddress.Normalize("2001:db8:1:2:aaaa:bbbb:cccc:dddd");
        var second = ClientAddress.Normalize("2001:db8:1:2::1");
        var otherNetwork = ClientAddress.Normalize("2001:db8:1:3::1");

        Assert.Equal("2001:db8:1:2::/64", first);
        Assert.Equal(first, second);
        Assert.NotEqual(first, otherNetwork);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not an address")]
    [InlineData("203.0.113.7, 198.51.100.1")]
    public void Anything_else_is_unknown(string? header) =>
        Assert.Equal(ClientAddress.Unknown, ClientAddress.Normalize(header));
}
