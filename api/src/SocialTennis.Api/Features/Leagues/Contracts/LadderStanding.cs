using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Team's line in its Division's Standings this Stage (#10), from the Scores so far.</summary>
/// <param name="Rank">1 for first. Teams the tie-breaks don't separate share one.</param>
/// <param name="Played">Its Games with a Score.</param>
/// <param name="Tied">Shares its rank: if that decides who moves, the Manager does (#10).</param>
/// <param name="Points">Its points, in a League ranked by bonus points (#214); null otherwise.</param>
/// <param name="Move">
/// Where it would go if the Stage closed now: up, down, or staying put. None
/// while nothing in the Division has a Score, or while a tie leaves it open.
/// </param>
public record LadderStanding(
    Guid TeamId, int Rank, int Played, int Won, int Lost, int GamesFor, int GamesAgainst, bool Tied, StandingMove? Move, int? Points = null);
