using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Roles;

namespace SocialTennis.Api.Features.Users;

/// <summary>
/// An Admin makes a User an Admin. It takes effect on that User's next request:
/// roles are read per request, not at sign-in. Granting a role already held
/// changes nothing. Managers are made per Club (MakeClubManager).
/// </summary>
public static class GrantRole
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid userId,
        string role,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        // The route matches the role in any case ("admin" too), so parse it the same way.
        var granted = Enum.Parse<UserRoles>(role, ignoreCase: true);

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
