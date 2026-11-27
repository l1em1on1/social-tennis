namespace SocialTennis.Api.Features.Games.Contracts;

/// <summary>One of a Game's two Sides: Side 1 is the Team higher on the ladder.</summary>
/// <param name="TeamId">Its Team; in Foursomes (#14, ADR-0036), the Team of one of its Player higher on the ladder.</param>
/// <param name="Players">One in singles, two in doubles, in the order they joined the Team. In Foursomes, the pair for this Game, the higher on the ladder first.</param>
/// <param name="PartnerTeamId">In Foursomes, the other Player's Team of one; null otherwise.</param>
public record GameSideSummary(int Side, Guid TeamId, IReadOnlyList<GamePlayer> Players, Guid? PartnerTeamId = null);
