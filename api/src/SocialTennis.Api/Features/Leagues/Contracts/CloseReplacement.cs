namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>
/// At the close, who takes a withdrawn Team's place in the next Stage (#21): a Team off the ladder,
/// in the place the withdrawn one would have moved to.
/// </summary>
/// <param name="TeamId">The Team that has withdrawn.</param>
/// <param name="ReplacementTeamId">The Team taking its place: in the League, complete, and off the ladder.</param>
public record CloseReplacement(Guid TeamId, Guid ReplacementTeamId);
