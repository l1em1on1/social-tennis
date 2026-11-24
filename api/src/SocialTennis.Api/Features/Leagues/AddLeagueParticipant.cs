using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Membership;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Manager puts a member of the League's Club straight into it, with no Join
/// Request; the add time is their join time. In a singles League they get a
/// Team of one; in a doubles League they are unpaired (<see cref="LeagueTeams"/>). A PUT: adding a participant again
/// changes nothing.
/// </summary>
/// <remarks>
/// If the Player has a request still pending, the add approves it, and they
/// keep the earlier request time as their join time: they asked first. If it
/// names a partner, the partner comes in with them as a Team, as approving
/// would (#6). A rejected request stays rejected; the add overrides it. A
/// Player named as someone's partner is added alone, unpaired: approving that
/// request later pairs them.
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
        // The Player's admission lock first, as deleting their User takes it (#116), then the League's:
        // an add made while they're being deleted waits, then finds them in no Club (#228 review).
        var email = await db.Players
            .Where(p => p.Id == playerId && p.User != null)
            .Select(p => p.User!.Email)
            .SingleOrDefaultAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (email is not null)
        {
            await ClubAdmission.TakeEmailLockAsync(db, email, cancellationToken);
        }

        await LeagueEntry.TakeLeagueLockAsync(db, leagueId, cancellationToken);
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
        if (pending is null)
        {
            LeagueTeams.AddParticipant(db, league, playerId, now, now);
        }
        else if (await LeagueEntry.ApproveAsync(db, league, pending, now, cancellationToken) is { } problem)
        {
            return problem;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
