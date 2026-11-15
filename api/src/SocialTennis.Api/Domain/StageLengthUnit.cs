using System.Text.Json.Serialization;

namespace SocialTennis.Api.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<StageLengthUnit>))]
public enum StageLengthUnit
{
    Weeks = 1,

    /// <summary>Calendar months: a Stage starting on the 1st ends on the last day of that month.</summary>
    Months = 2,
}
