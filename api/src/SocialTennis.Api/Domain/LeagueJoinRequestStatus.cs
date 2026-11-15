using System.Text.Json.Serialization;

namespace SocialTennis.Api.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<LeagueJoinRequestStatus>))]
public enum LeagueJoinRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}
