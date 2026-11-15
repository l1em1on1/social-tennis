using System.Text.Json.Serialization;

namespace SocialTennis.Api.Domain;

/// <summary>How a League's competition is organized (see CONTEXT.md, League Structure).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LeagueStructure>))]
public enum LeagueStructure
{
    /// <summary>A ladder of fixed-size Divisions with Relegation every Stage (ADR-0013).</summary>
    Divisional = 1,

    /// <summary>A single-elimination Bracket, one Stage per round.</summary>
    Knockout = 2,
}
