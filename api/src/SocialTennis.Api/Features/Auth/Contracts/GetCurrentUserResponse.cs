namespace SocialTennis.Api.Features.Auth.Contracts;

/// <param name="Admin">An Admin, by grant or by configuration.</param>
/// <param name="Manages">
/// Manages at least one Club (an Admin manages them all), so there is something
/// for them on Manage. What they may do with a Club, League or Game is said there.
/// </param>
public record GetCurrentUserResponse(Guid UserId, string Email, bool Admin, bool Manages);
