namespace SocialTennis.Api.Authentication;

/// <summary>
/// The role names in a session's claims. Managing isn't one: it is about a
/// Club, so <see cref="Access.ManagerAccess"/> reads it when it is asked (#110).
/// </summary>
public static class SessionRoles
{
    public const string Admin = "Admin";
}
