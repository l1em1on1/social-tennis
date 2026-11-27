namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Competitor on a Stage's ladder (#7): a Team, with its Players.</summary>
/// <param name="Position">Its place on the ladder, 1 at the top.</param>
/// <param name="Withdrawn">Its withdrawal was accepted (#21): it plays no more Games, and leaves the ladder at the close.</param>
public record LadderCompetitor(Guid TeamId, int Position, IReadOnlyList<LadderPlayer> Players, bool Withdrawn = false);
