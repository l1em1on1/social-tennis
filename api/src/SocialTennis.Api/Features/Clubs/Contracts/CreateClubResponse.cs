namespace SocialTennis.Api.Features.Clubs.Contracts;

/// <summary>The Club just created. Nobody is in it yet.</summary>
public record CreateClubResponse(Guid Id, string Name);
