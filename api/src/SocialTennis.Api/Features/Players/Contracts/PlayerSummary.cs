using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Players.Contracts;

/// <summary>
/// One of the signed-in User's Players, with the Clubs it belongs to. No
/// Rating: that is shown to Managers only (issue #4).
/// </summary>
public record PlayerSummary(Guid Id, string Name, PlayerGender? Gender, int? Level, IReadOnlyList<PlayerClub> Clubs);
