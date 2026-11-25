namespace SocialTennis.Api.Authentication;

/// <summary>The role names in a session's claims and in /auth/me. An Admin's session carries both.</summary>
public static class SessionRoles
{
    public const string Manager = "Manager";

    public const string Admin = "Admin";
}
