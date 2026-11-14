namespace SocialTennis.Api.Authentication;

/// <summary>
/// Named authorization policies, attached in each feature's routing table
/// (ADR-0017). Also the role names in a session's claims and in /auth/me.
/// </summary>
public static class Policies
{
    /// <summary>Managers, and Admins: an Admin's session carries both roles.</summary>
    public const string Manager = "Manager";

    public const string Admin = "Admin";
}
