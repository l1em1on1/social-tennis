namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>
/// What starting a League would do (#7): the proposed ladder, by Rating, then
/// earliest join time (ADR-0013), split into Divisions of about the League's
/// size (ADR-0031); the Players it would remove, still without a partner in a
/// doubles League (owner, 2026-11-20); the Join Requests still waiting, which
/// stay as reserves; and the first Stage's dates.
/// </summary>
/// <param name="Competitors">Every Team, in the proposed order, top first.</param>
/// <param name="Unpaired">Doubles Players without a partner: starting removes them from the League.</param>
/// <param name="DivisionSize">The League's Teams per Division, which the proposal aims for.</param>
/// <param name="Divisions">The proposed Division sizes, top first, adding up to the Competitors; empty with fewer than two.</param>
/// <param name="WaitingRequestCount">Join Requests still waiting: approving one after the start brings the Player in without a place.</param>
public record GetLeagueStartResponse(
    IReadOnlyList<LeagueStartCompetitor> Competitors,
    int DivisionSize,
    IReadOnlyList<int> Divisions,
    IReadOnlyList<LeagueStartPlayer> Unpaired,
    int WaitingRequestCount,
    LeagueStageSummary FirstStage);
