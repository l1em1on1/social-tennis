using System.Text.Json.Serialization;

namespace SocialTennis.Api.Domain;

/// <summary>What a User's role in a Club lets them do there.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClubRoleKind>))]
public enum ClubRoleKind
{
    /// <summary>Runs the Club: its members, its Leagues and who manages those.</summary>
    Manager = 1,
}
