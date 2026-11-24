using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Player in a League, as a Manager sees them.</summary>
/// <param name="Email">The email of the User holding the Player; none for a Former player, whose User was deleted (#116).</param>
/// <param name="JoinedAt">
/// The request time for an approved request, the add time for a Manager's add.
/// Breaks ties in the proposed starting ladder (#7).
/// </param>
/// <param name="Gender">Shown when the Manager pairs Players for mixed doubles (#6).</param>
/// <param name="TeamId">
/// Their Team: always one in singles; in doubles, none until they are paired;
/// never in Foursomes.
/// </param>
public record LeagueParticipantSummary(
    Guid PlayerId,
    string Name,
    string? Email,
    int Rating,
    DateTimeOffset JoinedAt,
    PlayerGender? Gender,
    Guid? TeamId);
