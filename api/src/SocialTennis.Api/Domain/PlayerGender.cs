using System.Text.Json.Serialization;

namespace SocialTennis.Api.Domain;

/// <summary>
/// What a Player plays as, for League Types that are men's, women's or mixed
/// (see CONTEXT.md, League Type). On the wire it is the name, not the number.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlayerGender>))]
public enum PlayerGender
{
    Male = 1,
    Female = 2,
}
