using System.Text.Json.Serialization;

namespace SocialTennis.Api.Domain;

/// <summary>
/// How a <see cref="LeagueStructure.Divisional"/> League of Teams ranks its
/// Standings (#214, ADR-0035), chosen when it is created and fixed after.
/// Either way, ties go by games difference, then head-to-head, then the Manager.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<StandingsMode>))]
public enum StandingsMode
{
    /// <summary>Matches won first.</summary>
    Standard = 1,

    /// <summary>
    /// Points first: 1 for a match won, 1 for a match played, and 1 more for
    /// playing every Game of the Stage in the Division.
    /// </summary>
    BonusPoints = 2,
}
