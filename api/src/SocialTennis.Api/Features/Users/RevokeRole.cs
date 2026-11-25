using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Roles;

namespace SocialTennis.Api.Features.Users;

/// <summary>
/// An Admin takes the Admin role from a User, effective on that User's next
/// request. Removing a role not held changes nothing.
/// </summary>
/// <remarks>
/// Removing Admin is refused with 409 when it could not take effect (the email
/// is an Admin by configuration) or would leave no Admin at all. An Admin may
/// remove their own role, under the same rules.
/// </remarks>
public static class RevokeRole
{
    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
        Guid userId,
        string role,
        TennisDbContext db,
        IOptions<AuthOptions> authOptions,
        CancellationToken cancellationToken)
    {
        // The route matches the role in any case ("admin" too), so parse it the same way.
        var revoked = Enum.Parse<UserRoles>(role, ignoreCase: true);
        var auth = authOptions.Value;

        await using var transaction = await AdminRoleRules.LockRolesAsync(db, cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        if (revoked == UserRoles.Admin)
        {
            var otherGrantedAdmins = await db.Users
                .CountAsync(u => u.Id != userId && (u.Roles & UserRoles.Admin) != 0, cancellationToken);
            var refusal = AdminRoleRules.CanRemoveAdmin(
                auth.IsAdminEmail(user.Email), auth.HasAdminEmails, otherGrantedAdmins);

            // The last-Admin rule only protects someone who is one.
            if (refusal == AdminRoleRules.Refusal.AdminBySetting
                || (refusal == AdminRoleRules.Refusal.LastAdmin && user.Roles.HasFlag(UserRoles.Admin)))
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "The Admin role can't be removed from this User.",
                    detail: refusal == AdminRoleRules.Refusal.AdminBySetting
                        ? "Their email is an Admin in the app's configuration. Change it where the app is deployed."
                        : "They are the only Admin. Make someone else an Admin first.");
            }
        }

        user.Roles &= ~revoked;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
