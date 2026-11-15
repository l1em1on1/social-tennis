using System.Net;
using System.Net.Sockets;

namespace SocialTennis.Api.Authentication;

/// <summary>
/// The client a request came from, as the BFF reports it (#108). Every request
/// reaches the API from the BFF over the environment's internal ingress, so the
/// API never sees a browser's address itself. The BFF sends the address the
/// platform's ingress observed in <see cref="HeaderName"/>; the API reads only
/// that header, never X-Forwarded-For.
/// </summary>
public static class ClientAddress
{
    /// <summary>Set by the BFF on calls made for a browser: the browser's address.</summary>
    public const string HeaderName = "X-Client-Address";

    /// <summary>
    /// Every request without a usable address shares this one key, so a missing
    /// header gets the tightest limit rather than none.
    /// </summary>
    public const string Unknown = "unknown";

    /// <summary>The key this request's client is limited and counted by.</summary>
    public static string KeyOf(HttpContext context) => Normalize(context.Request.Headers[HeaderName].ToString());

    /// <summary>
    /// An IPv4 address as itself; an IPv6 address as its /64 network, because
    /// one connection is usually handed a whole /64 and could otherwise use a
    /// new address for every request. Anything that isn't an address is
    /// <see cref="Unknown"/>.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (!IPAddress.TryParse(value?.Trim(), out var address))
        {
            return Unknown;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"{new IPAddress(bytes)}/64";
    }
}
