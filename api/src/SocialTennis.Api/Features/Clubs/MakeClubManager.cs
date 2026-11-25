using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Roles;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// An Admin makes a User a manager of this Club (#110), on that User's next
/// request. Any User: a manager needn't play. Making one again changes nothing.
/// </summary>
public static class MakeClubManager
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid clubId,
        Guid userId,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        // The lock deleting a User takes first (#116), so a role can't be given to one as they go.
        await using var transaction = await AdminRoleRules.LockRolesAsync(db, cancellationToken);
        if (!await db.Clubs.AnyAsync(c => c.Id == clubId, cancellationToken)
            || !await db.Users.AnyAsync(u => u.Id == userId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        if (!await db.ClubRoles.AnyAsync(r => r.ClubId == clubId && r.UserId == userId, cancellationToken))
        {
            db.ClubRoles.Add(new ClubRole
            {
                ClubId = clubId,
                UserId = userId,
                Kind = ClubRoleKind.Manager,
                GrantedAt = clock.GetUtcNow(),
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
