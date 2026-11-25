using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Clubs.Contracts;

/// <summary>A Player in a Club, or asking to be, as a Manager sees them.</summary>
/// <param name="UserId">The User holding the Player: who a role is given to (#220).</param>
/// <param name="Email">The email of that User.</param>
/// <param name="SignedIn">
/// Whether that User has ever signed in. False for a Player a Manager added for
/// someone who hasn't arrived yet.
/// </param>
/// <param name="Approved">False while the Player's request waits for a Manager.</param>
/// <param name="Rating">Player Rating, which a Manager sets (ADR-0007).</param>
/// <param name="RequestedAt">When the Player asked to join, or was let in or added by a Manager.</param>
public record ClubMember(
    Guid PlayerId,
    Guid UserId,
    string Name,
    string Email,
    bool SignedIn,
    bool Approved,
    PlayerGender? Gender,
    int? Level,
    int Rating,
    DateTimeOffset RequestedAt);
