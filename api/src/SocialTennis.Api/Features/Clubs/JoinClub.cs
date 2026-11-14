using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// One of the signed-in User's Players joins a Club. Open to anyone signed in:
/// no approval, as issue #4 specifies. A PUT, so joining twice is the same as
/// joining once.
/// </summary>
public static class JoinClub
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid clubId,
        Guid playerId,
        ClaimsPrincipal user,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        var held = await db.Players.AnyAsync(p => p.Id == playerId && p.UserId == userId, cancellationToken);
        if (!held || !await db.Clubs.AnyAsync(c => c.Id == clubId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        if (!await db.ClubMemberships.AnyAsync(m => m.ClubId == clubId && m.PlayerId == playerId, cancellationToken))
        {
            db.ClubMemberships.Add(new ClubMembership
            {
                ClubId = clubId,
                PlayerId = playerId,
                JoinedAt = clock.GetUtcNow(),
            });
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Two presses raced; the other one's row is the membership.
            }
        }

        return TypedResults.NoContent();
    }
}
