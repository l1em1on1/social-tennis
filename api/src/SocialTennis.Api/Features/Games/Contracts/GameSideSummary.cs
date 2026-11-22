namespace SocialTennis.Api.Features.Games.Contracts;

/// <summary>One of a Game's two Sides: Side 1 is the Team higher on the ladder.</summary>
/// <param name="Players">One in singles, two in doubles, in the order they joined the Team.</param>
public record GameSideSummary(int Side, Guid TeamId, IReadOnlyList<GamePlayer> Players);
