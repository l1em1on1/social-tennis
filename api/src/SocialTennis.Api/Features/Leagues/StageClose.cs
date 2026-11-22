using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>What closing a Stage reads, for its screen and for the close itself (#10).</summary>
public static class StageClose
{
    /// <summary>
    /// Teams in the League but off the current ladder, complete for its Type
    /// (a doubles Team of two), which the Manager can add at close.
    /// </summary>
    public static async Task<List<CloseWaitingTeam>> WaitingAsync(
        TennisDbContext db, Guid leagueId, Guid stageId, LeagueType type, CancellationToken cancellationToken)
    {
        var size = type.TeamSize();
        return await db.Teams
            .Where(t => t.LeagueId == leagueId && t.Members.Count == size && !db.LadderPlaces.Any(p => p.StageId == stageId && p.TeamId == t.Id))
            .OrderBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Select(t => new CloseWaitingTeam(
                t.Id,
                t.Members.OrderBy(m => m.JoinedAt).ThenBy(m => m.PlayerId).Select(m => new LadderPlayer(m.PlayerId, m.Player.Name)).ToList()))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public static ProblemHttpResult AlreadyClosed() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "stage-closed",
            title: "This League's last Stage has closed: the League has ended.");

    /// <summary>ADR-0017's own example: a Stage can't close while anything waits on the Manager.</summary>
    public static ProblemHttpResult QueueNotEmpty() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "stage-queue-not-empty",
            title: "Games with no score are holding the Stage up.",
            detail: "Record each one's score, or mark it not played, then close the Stage.");

    public static ProblemHttpResult StandingsChanged() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "standings-changed",
            title: "The Standings have changed since the close screen loaded.",
            detail: "A score was entered or changed in the meantime. Look at the close again.");

    public static ProblemHttpResult LeagueChanged() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "league-changed",
            title: "The Teams waiting for a place have changed since the close screen loaded.",
            detail: "Look at the close again.");
}
