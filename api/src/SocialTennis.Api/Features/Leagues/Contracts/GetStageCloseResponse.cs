namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>
/// Closing the current Stage, as the Manager sees it before confirming (#10;
/// wireframe M6): its Divisions with their Standings, what still waits on
/// them, the Teams waiting for a place, and the next Stage's dates.
/// </summary>
/// <param name="LeagueEndDate">The League's last day: no Stage runs past it.</param>
/// <param name="HeldUp">Past its deadline: closing is due (ADR-0016). A Manager can close earlier.</param>
/// <param name="Divisions">As on the ladder: each Division's Teams, Games and Standings.</param>
/// <param name="Awaiting">
/// Games nobody scored, which must be settled before the close, and the ties
/// that decide who moves, which the Manager orders when they confirm.
/// </param>
/// <param name="Waiting">Teams off the ladder that can join at the bottom of the bottom Division.</param>
/// <param name="NextStage">None when this Stage is the League's last: closing it ends the League, and nobody moves.</param>
public record GetStageCloseResponse(
    LeagueStageSummary Stage,
    DateOnly LeagueEndDate,
    bool HeldUp,
    IReadOnlyList<LadderDivision> Divisions,
    LadderAwaiting Awaiting,
    IReadOnlyList<CloseWaitingTeam> Waiting,
    CloseNextStage? NextStage);
