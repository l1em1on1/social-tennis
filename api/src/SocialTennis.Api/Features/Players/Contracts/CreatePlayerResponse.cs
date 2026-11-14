namespace SocialTennis.Api.Features.Players.Contracts;

/// <summary>The Player just created. It belongs to no Club yet.</summary>
public record CreatePlayerResponse(Guid Id, string Name);
