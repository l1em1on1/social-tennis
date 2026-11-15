using System.Text.Json.Serialization;

namespace SocialTennis.Api.Domain;

/// <summary>
/// How a <see cref="LeagueStructure.Divisional"/> League's Stages close
/// (ADR-0013, ADR-0016). Either way a Stage stays open while anything awaits
/// the Manager's resolution.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<StageRoll>))]
public enum StageRoll
{
    /// <summary>Reaching EndDate tries to close the Stage; Relegation runs and the next Stage opens.</summary>
    Automatic = 1,

    /// <summary>The Stage stays open, Scores editable, until the Manager closes it.</summary>
    Manual = 2,
}
