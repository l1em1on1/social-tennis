using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Membership;
using SocialTennis.Api.Roles;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// A Club's manager makes someone with a Player in the Club a League manager
/// (#220): they create Leagues there, and run those and any they are assigned.
/// One of its managers is left as they are.
/// </summary>
public static class GrantLeagueManagerRole
{
    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
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

        if (!await ClubMembers.HasPlayerAsync(db, clubId, userId, cancellationToken))
        {
            return ClubMembers.NotInClub();
        }

        if (!await db.ClubRoles.AnyAsync(r => r.ClubId == clubId && r.UserId == userId, cancellationToken))
        {
            db.ClubRoles.Add(new ClubRole
            {
                ClubId = clubId,
                UserId = userId,
                Kind = ClubRoleKind.LeagueManager,
                GrantedAt = clock.GetUtcNow(),
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
