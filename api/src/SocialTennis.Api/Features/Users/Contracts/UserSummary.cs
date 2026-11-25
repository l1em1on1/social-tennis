namespace SocialTennis.Api.Features.Users.Contracts;

/// <summary>A User as an Admin sees them: who they are and what they may do.</summary>
/// <param name="Clubs">Their roles in Clubs, by Club name. An Admin can manage every Club without one.</param>
/// <param name="Admin">An Admin, by grant or because their email is in Auth:AdminEmails.</param>
/// <param name="AdminBySetting">
/// Their email is in Auth:AdminEmails. That can only be changed where the app is deployed,
/// so the Admin role can't be removed from them here.
/// </param>
/// <param name="LastAdmin">
/// The only Admin there is, with no address in Auth:AdminEmails to fall back on. Removing
/// their Admin role is refused, so the app is never left with nobody able to act.
/// </param>
public record UserSummary(
    Guid Id,
    string Email,
    IReadOnlyList<UserClubRole> Clubs,
    bool Admin,
    bool AdminBySetting,
    bool LastAdmin,
    IReadOnlyList<UserPlayer> Players);
