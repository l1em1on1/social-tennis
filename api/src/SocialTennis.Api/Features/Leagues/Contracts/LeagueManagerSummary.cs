namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>Someone who runs a League, for whoever else does (#220).</summary>
/// <param name="UserId">Who they are, for taking the League from them.</param>
/// <param name="Name">Their Player in the League's Club; null if they have none there.</param>
/// <param name="Email">Only for a manager of the Club (ADR-0024); null otherwise.</param>
public record LeagueManagerSummary(Guid UserId, string? Name, string? Email);
