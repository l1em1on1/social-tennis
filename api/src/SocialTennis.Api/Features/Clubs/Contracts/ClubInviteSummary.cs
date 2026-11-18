namespace SocialTennis.Api.Features.Clubs.Contracts;

/// <summary>
/// An email a Manager has invited to a Club, which hasn't let anyone in yet:
/// an invite is used up as it does (#164).
/// </summary>
public record ClubInviteSummary(Guid Id, string Email, DateTimeOffset InvitedAt);
