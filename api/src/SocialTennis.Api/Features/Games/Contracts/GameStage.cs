namespace SocialTennis.Api.Features.Games.Contracts;

/// <summary>The Stage a Game is played in.</summary>
/// <param name="EndDate">The deadline: it passes at the end of this day, in the Club's time zone (ADR-0016).</param>
/// <param name="Closed">Closed (#10): its Players can't change a Score any more, and a Manager can still override one.</param>
public record GameStage(int Number, DateOnly StartDate, DateOnly EndDate, bool Closed);
