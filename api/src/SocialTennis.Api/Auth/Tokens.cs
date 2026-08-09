using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace SocialTennis.Api.Auth;

public static class Tokens
{
    /// <summary>256-bit random token, base64url — safe in a query string.</summary>
    public static string NewToken() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// SHA-256 hex of the raw token — the only form ever persisted, so a
    /// database leak doesn't leak redeemable links or live sessions.
    /// </summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
