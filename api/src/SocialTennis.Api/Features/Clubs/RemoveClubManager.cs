using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Roles;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// An Admin takes a User's role as manager of this Club (#110), on that User's
/// next request. One who isn't a manager there changes nothing.
/// </summary>
public static class RemoveClubManager
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
            .Where(r => r.ClubId == clubId && r.UserId == userId && r.Kind == ClubRoleKind.Manager)
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
