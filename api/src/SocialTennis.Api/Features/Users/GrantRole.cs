using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Roles;

namespace SocialTennis.Api.Features.Users;

/// <summary>
/// An Admin makes a User a Manager or an Admin. It takes effect on that User's
/// next request: roles are read per request, not at sign-in. Granting a role
/// already held changes nothing.
/// </summary>
public static class GrantRole
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid userId,
        string role,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var granted = Enum.Parse<UserRoles>(role);

        await using var transaction = await AdminRoleRules.LockRolesAsync(db, cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        user.Roles |= granted;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
