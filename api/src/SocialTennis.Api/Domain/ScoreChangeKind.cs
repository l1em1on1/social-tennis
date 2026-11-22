using System.Text.Json.Serialization;

namespace SocialTennis.Api.Domain;

/// <summary>How a Score changed (ADR-0008, Phasing). Corrections and overrides are #38's second part.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScoreChangeKind>))]
public enum ScoreChangeKind
{
    /// <summary>A Player who played the Game entered it.</summary>
    Entered = 1,
}
