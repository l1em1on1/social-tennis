using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Roles;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A manager of the League's Club takes a League from someone who runs it
/// (#220). Their role in the Club stays. One who doesn't run it changes nothing.
/// </summary>
public static class UnassignLeagueManager
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid leagueId,
        Guid userId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        await using var transaction = await AdminRoleRules.LockRolesAsync(db, cancellationToken);
        if (!await db.Leagues.AnyAsync(l => l.Id == leagueId, cancellationToken)
            || !await db.Users.AnyAsync(u => u.Id == userId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        await db.LeagueManagers
            .Where(m => m.LeagueId == leagueId && m.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
