namespace SocialTennis.Api.Contracts;

/// <summary>
/// A manager of a League, as its Players see them: never an email (ADR-0024).
/// </summary>
/// <param name="Name">Their Player in the League's Club; null if they have none there.</param>
public record LeagueManagerName(string? Name);
