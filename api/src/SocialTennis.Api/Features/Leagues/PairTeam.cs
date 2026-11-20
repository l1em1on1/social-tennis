using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Manager makes a Team of two unpaired Players in a doubles League (#6).
/// They then compete together for the life of the League.
/// </summary>
/// <remarks>
/// A League that isn't doubles is a 409: a singles participant already has a
/// Team of one, and a Foursomes League has no Teams. So is a Player who isn't
/// in the League or already has a partner. Leagues can't start yet (#7); once
/// they can, pairing is refused after the start.
/// </remarks>
public static class PairTeam
{
    public static async Task<Results<Created<PairTeamResponse>, NotFound, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
        PairTeamRequest request,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        await using var transaction = await LeagueEntry.LockLeagueAsync(db, leagueId, cancellationToken);
        var type = await db.Leagues
            .Where(l => l.Id == leagueId)
            .Select(l => (LeagueType?)l.Type)
            .SingleOrDefaultAsync(cancellationToken);
        if (type is null)
        {
            return TypedResults.NotFound();
        }
        if (type.Value.TeamSize() != 2)
        {
            return NotDoubles();
        }
        var playerIds = new[] { request.FirstPlayerId, request.SecondPlayerId };
        var unpaired = await db.LeagueParticipants
            .CountAsync(p => p.LeagueId == leagueId && playerIds.Contains(p.PlayerId) && p.TeamId == null, cancellationToken);
        if (unpaired != 2)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "not-unpaired",
                title: "Both Players must be in the League without a partner.",
                detail: "The lists below are up to date.");
        }
        var team = new Team { Id = Guid.NewGuid(), LeagueId = leagueId, CreatedAt = clock.GetUtcNow() };
        db.Teams.Add(team);
        await db.SaveChangesAsync(cancellationToken);
        await db.LeagueParticipants
            .Where(p => p.LeagueId == leagueId && playerIds.Contains(p.PlayerId))
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.TeamId, team.Id), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.Created((string?)null, new PairTeamResponse(team.Id));
    }

    internal static ProblemHttpResult NotDoubles() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "not-a-doubles-league",
            title: "Only a doubles League's Players are paired by the Manager.",
            detail: "In a singles League each Player is a Team of one; a Foursomes League has no Teams.");
}
