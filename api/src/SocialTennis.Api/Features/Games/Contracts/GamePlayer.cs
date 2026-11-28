namespace SocialTennis.Api.Features.Games.Contracts;

/// <summary>A Player on a Game's side.</summary>
/// <param name="Withdrawn">Their Team has withdrawn from the League (#21). In Foursomes, only the Player who left: their partner for the Game hasn't.</param>
public record GamePlayer(Guid PlayerId, string Name, bool Withdrawn = false);
