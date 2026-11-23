using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Membership;

/// <summary>
/// A Club's join link code (#222, ADR-0032): 128 random bits in base64url, so
/// it can't be guessed and reads cleanly in a URL. Managers see it as it is,
/// since it only lets a Player ask to join.
/// </summary>
public static partial class JoinCodes
{
    public static string New() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(16));

    /// <summary>
    /// Whether <paramref name="code"/> has a code's shape (<see cref="Club.JoinCodeLength"/>
    /// base64url characters), for a sign-in request carrying one back to its link.
    /// </summary>
    public static bool IsWellFormed(string? code) => code is not null && Shape().IsMatch(code);

    [GeneratedRegex("^[A-Za-z0-9_-]{22}$")]
    private static partial Regex Shape();
}
