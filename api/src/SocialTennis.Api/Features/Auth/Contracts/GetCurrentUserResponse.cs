namespace SocialTennis.Api.Features.Auth.Contracts;

/// <param name="Admin">An Admin, by grant or by configuration.</param>
/// <param name="Manages">
/// Manages something: a Club, a League, or as a League manager one to come (#220; an Admin manages
/// them all), so there is something for them on Manage. What they may do with a Club, League or Game
/// is said there.
/// </param>
/// <param name="ManagesClub">Manages a Club, so can let people into it (an Admin manages every Club).</param>
public record GetCurrentUserResponse(Guid UserId, string Email, bool Admin, bool Manages, bool ManagesClub);
