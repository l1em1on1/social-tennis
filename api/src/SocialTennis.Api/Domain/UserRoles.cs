namespace SocialTennis.Api.Domain;

/// <summary>
/// Permission roles a User holds everywhere (see CONTEXT.md). Managers aren't
/// here: they belong to a Club (<see cref="ClubRole"/>, #110).
/// </summary>
/// <remarks>1 was Manager, for every Club, until #110's migration.</remarks>
[Flags]
public enum UserRoles
{
    None = 0,
    Admin = 2,
}
