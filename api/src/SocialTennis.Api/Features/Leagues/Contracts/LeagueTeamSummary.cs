namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Team in a League, as a Manager sees it (#6), and its place on the current Stage's ladder (#7).</summary>
/// <param name="PlayerIds">Its Players, as in the League's participants: one in singles, two in doubles.</param>
/// <param name="Rating">
/// The average of its Players' Ratings, worked out on read; a Team has no
/// Rating of its own (CONTEXT.md, Team).
/// </param>
/// <param name="Position">Its place on the current Stage's ladder, 1 at the top; null if it isn't on it (left out, or came in after the start) or the League hasn't started.</param>
/// <param name="Division">Its Division on that ladder (ADR-0031); null with no place.</param>
/// <param name="Withdrawn">Its withdrawal was accepted (#21): it leaves the ladder at the close.</param>
public record LeagueTeamSummary(Guid Id, IReadOnlyList<Guid> PlayerIds, double Rating, int? Position, int? Division, bool Withdrawn = false);
