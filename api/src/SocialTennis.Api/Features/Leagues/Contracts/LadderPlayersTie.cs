using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>
/// A Foursomes tie the Division's Players settle themselves (#14 part 3, owner,
/// 2026-11-26): Players level on games at a place that moves, once all 3 Games
/// have a Score and none was marked not played.
/// </summary>
/// <param name="Place">Up: who goes up from first. Down: who goes down from last.</param>
/// <param name="TeamIds">The Players' Teams of one level there, in Standings order.</param>
/// <param name="Mark">Who they said, once one of them has; the last answer stands.</param>
public record LadderPlayersTie(TieMarkPlace Place, IReadOnlyList<Guid> TeamIds, LadderTieMark? Mark);
