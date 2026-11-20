using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Manager puts a member of the League's Club straight into it, with no Join
/// Request; the add time is their join time. In a singles League they get a
/// Team of one; in a doubles League they are unpaired (<see cref="LeagueTeams"/>). A PUT: adding a participant again
/// changes nothing.
/// </summary>
/// <remarks>
/// If the Player has a request still pending, the add approves it, and they
/// keep the earlier request time as their join time: they asked first. A
/// rejected request stays rejected; the add overrides it.
/// </remarks>
public static class AddLeagueParticipant
{
    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
        Guid playerId,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        await using var transaction = await LeagueEntry.LockLeagueAsync(db, leagueId, cancellationToken);
        var league = await db.Leagues.SingleOrDefaultAsync(l => l.Id == leagueId, cancellationToken);
        if (league is null || !await db.Players.AnyAsync(p => p.Id == playerId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        if (await LeagueEntry.IsParticipantAsync(db, leagueId, playerId, cancellationToken))
        {
            return TypedResults.NoContent();
        }

        if (!await LeagueEntry.IsClubMemberAsync(db, league.ClubId, playerId, cancellationToken))
        {
            return LeagueEntry.NotAClubMember();
        }

        var now = clock.GetUtcNow();
        var pending = await db.LeagueJoinRequests.SingleOrDefaultAsync(
            r => r.LeagueId == leagueId && r.PlayerId == playerId && r.Status == LeagueJoinRequestStatus.Pending,
            cancellationToken);
        if (pending is not null)
        {
            pending.Status = LeagueJoinRequestStatus.Approved;
            pending.DecidedAt = now;
        }

        LeagueTeams.AddParticipant(db, league, playerId, pending?.RequestedAt ?? now, now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
