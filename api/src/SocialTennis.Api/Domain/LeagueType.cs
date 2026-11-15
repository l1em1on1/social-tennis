using System.Text.Json.Serialization;

namespace SocialTennis.Api.Domain;

/// <summary>
/// Who plays in a League (see CONTEXT.md, League Type). Independent of
/// <see cref="LeagueStructure"/>, except that <see cref="Foursomes"/> is
/// <see cref="LeagueStructure.Divisional"/> only. On the wire it is the name.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<LeagueType>))]
public enum LeagueType
{
    SinglesMen = 1,
    SinglesWomen = 2,
    SinglesMixed = 3,
    DoublesMen = 4,
    DoublesWomen = 5,
    DoublesMix = 6,
    Foursomes = 7,
}
