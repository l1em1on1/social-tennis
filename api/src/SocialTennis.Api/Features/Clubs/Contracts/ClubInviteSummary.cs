namespace SocialTennis.Api.Features.Clubs.Contracts;

/// <summary>An email a Manager has invited to a Club.</summary>
/// <param name="Joined">Whether a Player held by that email is in the Club yet.</param>
public record ClubInviteSummary(string Email, bool Joined, DateTimeOffset InvitedAt);
