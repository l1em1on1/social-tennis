namespace SocialTennis.Api.Features.Players.Contracts;

/// <summary>One of the signed-in User's Players, with the Clubs it belongs to.</summary>
public record PlayerSummary(Guid Id, string Name, IReadOnlyList<PlayerClub> Clubs);
