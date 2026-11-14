namespace SocialTennis.Api.Features.Clubs.Contracts;

/// <summary>A Player in a Club, or asking to be, as a Manager sees them.</summary>
/// <param name="Email">The email of the User holding the Player.</param>
/// <param name="Approved">False while the Player's request waits for a Manager.</param>
/// <param name="RequestedAt">When the Player asked to join, or was let in by an invite.</param>
public record ClubMember(Guid PlayerId, string Name, string Email, bool Approved, DateTimeOffset RequestedAt);
