namespace SocialTennis.Api.Features.Players.Contracts;

/// <summary>A Club one of the User's Players is in, or has asked to join.</summary>
/// <param name="Approved">False while the request waits for a Manager.</param>
public record PlayerClub(Guid Id, string Name, bool Approved);
