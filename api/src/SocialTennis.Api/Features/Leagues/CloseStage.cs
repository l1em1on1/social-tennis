using Microsoft.AspNetCore.Http.HttpResults;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Manager closes the current Stage (#10; see CONTEXT.md: Stage,
/// Relegation): it locks for its Players, Relegation moves each Division's
/// first up and last down (<see cref="Relegation.Next"/>), the Teams the
/// Manager adds join the bottom, and the next Stage opens with a Game for
/// every pair in each Division. Closing the League's last Stage ends it, and
/// nobody moves.
/// </summary>
/// <remarks>
/// <para>
/// The request says what the Manager confirmed: every Division in its final
/// order. That order must still follow the Standings, with ties in any order
/// the Manager chose. A Score entered or changed since, so that it no longer
/// does, is a 409 <c>standings-changed</c>, and the Teams joining must still
/// be waiting (409 <c>league-changed</c>), so nobody moves on a result the
/// Manager didn't see. The close holds the League's entry lock and its
/// results lock, so no Score lands between the check and the close.
/// </para>
/// <list type="bullet">
/// <item>No League, or one not started: 404. Its last Stage already closed: 409 <c>stage-closed</c>.</item>
/// <item>A Game with no Score that isn't marked not played: 409 <c>stage-queue-not-empty</c> (ADR-0017).</item>
/// <item>The next Stage's dates: from after this Stage ends to the League's end, or 400 under their wire name.
/// None, and no Teams joining, when this is the League's last Stage.</item>
/// </list>
/// </remarks>
public static class CloseStage
{
    public static async Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
        CloseStageRequest request,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var closed = await StageClose.CloseAsync(
            db, leagueId, request.Divisions, request.Joining, request.NextStartDate, request.NextEndDate, clock, cancellationToken,
            replacements: request.Replacements);
        return closed.Refusal switch
        {
            CloseRefusal.None => TypedResults.NoContent(),
            CloseRefusal.NotFound => TypedResults.NotFound(),
            CloseRefusal.AlreadyClosed => StageClose.AlreadyClosed(),
            CloseRefusal.QueueNotEmpty or CloseRefusal.TiesOpen => StageClose.QueueNotEmpty(),
            CloseRefusal.StandingsChanged => StageClose.StandingsChanged(),
            CloseRefusal.LeagueChanged => StageClose.LeagueChanged(),
            _ => TypedResults.ValidationProblem(closed.Errors!),
        };
    }
}
