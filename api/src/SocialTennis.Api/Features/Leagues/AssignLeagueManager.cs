using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Membership;
using SocialTennis.Api.Roles;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A manager of the League's Club gives someone this League to run (#220): any
/// User with a Player in that Club, with the League manager role or without it.
/// Assigning again changes nothing.
/// </summary>
public static class AssignLeagueManager
{
    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
        Guid leagueId,
        Guid userId,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        // The lock deleting a User takes first (#116), so a League can't be given to one as they go.
        await using var transaction = await AdminRoleRules.LockRolesAsync(db, cancellationToken);
        var clubId = await db.Leagues.Where(l => l.Id == leagueId).Select(l => (Guid?)l.ClubId).SingleOrDefaultAsync(cancellationToken);
        if (clubId is null || !await db.Users.AnyAsync(u => u.Id == userId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        if (!await ClubMembers.HasPlayerAsync(db, clubId.Value, userId, cancellationToken))
        {
            return ClubMembers.NotInClub();
        }

        if (!await db.LeagueManagers.AnyAsync(m => m.LeagueId == leagueId && m.UserId == userId, cancellationToken))
        {
            db.LeagueManagers.Add(new LeagueManager { LeagueId = leagueId, UserId = userId, AssignedAt = clock.GetUtcNow() });
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
