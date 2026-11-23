namespace SocialTennis.Api.Features.Clubs.Contracts;

/// <summary>The Club a working join link is for (#222), named on the page it opens.</summary>
public record GetJoinLinkClubResponse(Guid ClubId, string ClubName);
