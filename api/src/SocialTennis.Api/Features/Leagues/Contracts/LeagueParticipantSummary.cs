namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Player in a League, as a Manager sees them.</summary>
/// <param name="Email">The email of the User holding the Player.</param>
/// <param name="JoinedAt">
/// The request time for an approved request, the add time for a Manager's add.
/// Breaks ties in the proposed starting ladder (#7).
/// </param>
public record LeagueParticipantSummary(Guid PlayerId, string Name, string Email, int Rating, DateTimeOffset JoinedAt);
