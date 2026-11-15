using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Contracts;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Users.Contracts;
using SocialTennis.Api.Roles;

namespace SocialTennis.Api.Features.Users;

/// <summary>Every User, with their roles and Players, for an Admin granting roles. By email.</summary>
public static class GetUsers
{
    public static async Task<GetUsersResponse> HandleAsync(
        TennisDbContext db,
        IOptions<AuthOptions> authOptions,
        CancellationToken cancellationToken)
    {
        var auth = authOptions.Value;
        var rows = await db.Users
            .OrderBy(u => u.Email)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.Roles,
                Players = db.Players
                    .Where(p => p.UserId == u.Id)
                    .OrderBy(p => p.CreatedAt)
                    .Select(p => new UserPlayer(p.Id, p.Name))
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        var grantedAdmins = rows.Count(u => u.Roles.HasFlag(UserRoles.Admin));
        var users = rows
            .Select(u =>
            {
                var granted = u.Roles.HasFlag(UserRoles.Admin);
                var bySetting = auth.IsAdminEmail(u.Email);
                var refusal = AdminRoleRules.CanRemoveAdmin(bySetting, auth.HasAdminEmails, grantedAdmins - (granted ? 1 : 0));
                return new UserSummary(
                    u.Id,
                    u.Email,
                    Manager: u.Roles.HasFlag(UserRoles.Manager),
                    Admin: granted || bySetting,
                    AdminBySetting: bySetting,
                    LastAdmin: granted && refusal == AdminRoleRules.Refusal.LastAdmin,
                    u.Players);
            })
            .ToList();

        return new GetUsersResponse(users, new PageInfo(users.Count));
    }
}
