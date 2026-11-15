namespace SocialTennis.Api.Features.Clubs.Contracts;

/// <summary>The Player a Manager just added to the Club.</summary>
public record AddClubPlayerResponse(Guid PlayerId, string Name);
