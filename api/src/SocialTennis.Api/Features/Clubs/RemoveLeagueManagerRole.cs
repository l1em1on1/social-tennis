using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Roles;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// A Club's manager takes the League manager role (#220). Leagues they run stay
/// theirs, and are removed on each League. One who isn't a League manager
/// changes nothing, and neither does one of the Club's managers.
/// </summary>
public static class RemoveLeagueManagerRole
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid clubId,
        Guid userId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        await using var transaction = await AdminRoleRules.LockRolesAsync(db, cancellationToken);
        if (!await db.Clubs.AnyAsync(c => c.Id == clubId, cancellationToken)
            || !await db.Users.AnyAsync(u => u.Id == userId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        await db.ClubRoles
            .Where(r => r.ClubId == clubId && r.UserId == userId && r.Kind == ClubRoleKind.LeagueManager)
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
