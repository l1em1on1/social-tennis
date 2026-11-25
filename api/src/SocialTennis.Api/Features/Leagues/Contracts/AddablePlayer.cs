namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A member of the League's Club who isn't in the League, for whoever runs it to add (#220).</summary>
/// <param name="Email">Only for a manager of the Club: a League manager adds by name alone (ADR-0024). Null otherwise.</param>
public record AddablePlayer(Guid PlayerId, string Name, string? Email);
