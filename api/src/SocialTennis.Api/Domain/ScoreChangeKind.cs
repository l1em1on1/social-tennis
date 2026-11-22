using System.Text.Json.Serialization;

namespace SocialTennis.Api.Domain;

/// <summary>How a Score changed (ADR-0008, Phasing).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScoreChangeKind>))]
public enum ScoreChangeKind
{
    /// <summary>A Player who played the Game entered it.</summary>
    Entered = 1,

    /// <summary>A Player who played the Game corrected it while the Stage was open.</summary>
    Corrected = 2,

    /// <summary>A Manager overrode it, which they can do after the Stage closes too.</summary>
    Overridden = 3,
}
