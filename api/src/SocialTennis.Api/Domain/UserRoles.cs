namespace SocialTennis.Api.Domain;

/// <summary>
/// Permission roles held by a User (see CONTEXT.md). Club-wide in v1; per-Club
/// scoping is deferred (ADR-0003). An Admin can do everything a Manager can.
/// </summary>
[Flags]
public enum UserRoles
{
    None = 0,
    Manager = 1,
    Admin = 2,
}
