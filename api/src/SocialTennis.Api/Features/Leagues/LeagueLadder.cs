using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// The rules about a League's ladder that other endpoints share (#7): whether
/// the League has started, whether it can, and which Teams the ladder fixes.
/// </summary>
internal static class LeagueLadder
{
    /// <summary>A League has started once its first Stage is open.</summary>
    public static Task<bool> IsStartedAsync(TennisDbContext db, Guid leagueId, CancellationToken cancellationToken) =>
        db.Stages.AnyAsync(s => s.LeagueId == leagueId, cancellationToken);

    /// <summary>
    /// Whether a Team is on the League's ladder, or in one of its Games. Such a
    /// Team stays together and in the League: leaving is a Withdrawal (#21).
    /// Teams the Manager left out, or that came in after the start, aren't.
    /// </summary>
    public static async Task<bool> IsInPlayAsync(
        TennisDbContext db, Guid leagueId, Guid teamId, CancellationToken cancellationToken) =>
        await db.LadderPlaces.AnyAsync(p => p.LeagueId == leagueId && p.TeamId == teamId, cancellationToken)
        || await db.GameSides.AnyAsync(s => s.LeagueId == leagueId && s.TeamId == teamId, cancellationToken);

    /// <summary>
    /// A doubles League's Players still without a partner, whom starting it
    /// removes (owner, 2026-11-20). None in other Leagues.
    /// </summary>
    public static async Task<List<LeagueStartPlayer>> UnpairedAsync(
        TennisDbContext db, Guid leagueId, LeagueType type, CancellationToken cancellationToken) =>
        type.TeamSize() != 2
            ? []
            : await db.LeagueParticipants
                .Where(p => p.LeagueId == leagueId && p.TeamId == null)
                .OrderBy(p => p.JoinedAt)
                .ThenBy(p => p.PlayerId)
                .Select(p => new LeagueStartPlayer(p.PlayerId, p.Player.Name))
                .ToListAsync(cancellationToken);

    /// <summary>The 409 for breaking up, or removing a Player of, a Team in play.</summary>
    public static ProblemHttpResult InPlay() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "on-the-ladder",
            title: "This Team is on the League's ladder.",
            detail: "Once the League has started, a Team on its ladder stays together and in the League. Leaving it is a Withdrawal, which isn't built yet.");

    /// <summary>
    /// Why this League can't start, or null if it can (apart from who is in
    /// it): a League starts once, and only a Divisional League of the
    /// Team-based Types can start for now.
    /// </summary>
    public static async Task<ProblemHttpResult?> CannotStartAsync(
        TennisDbContext db, Guid leagueId, LeagueStructure structure, LeagueType type, CancellationToken cancellationToken)
    {
        if (await IsStartedAsync(db, leagueId, cancellationToken))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "already-started",
                title: "This League has already started.");
        }

        if (structure != LeagueStructure.Divisional)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "knockout-not-yet",
                title: "A Knockout League can't start yet.",
                detail: "Starting a Knockout League, with its Bracket, is still to be built.");
        }

        if (type.TeamSize() == 0)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "foursomes-not-yet",
                title: "A Foursomes League can't start yet.",
                detail: "Starting a Foursomes League, with its Divisions of 4, is still to be built.");
        }

        return null;
    }

    /// <summary>A mid-Stage move (#10) of a Team that isn't on the current ladder.</summary>
    public static ProblemHttpResult NotOnTheLadder() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "not-on-the-ladder",
            title: "This Team isn't on the ladder this Stage.",
            detail: "The Manager adds it when the Stage closes.");

    /// <summary>A mid-Stage move (#10) of a Team with a Score this Stage: results stay where they were played.</summary>
    public static ProblemHttpResult HasResults() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "team-has-results",
            title: "This Team has a result this Stage, so it can't move now.",
            detail: "Results stay where they were played: it moves at the close instead.");

    /// <summary>A mid-Stage move (#10) out of a Division of two, which would leave it one Team.</summary>
    public static ProblemHttpResult DivisionTooSmall(int division) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "division-too-small",
            title: $"Division {division} would be left with one Team.",
            detail: "A Division keeps at least two Teams.");
}
