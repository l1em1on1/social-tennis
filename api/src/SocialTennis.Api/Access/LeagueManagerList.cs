using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Contracts;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Access;

/// <summary>Who runs a League: those assigned to it (#220). None, when its Club's managers do.</summary>
public static class LeagueManagerList
{
    /// <summary>First assigned first, each named by their Player in the League's Club, if they have one there.</summary>
    public static async Task<IReadOnlyList<LeagueManagerRow>> OfAsync(TennisDbContext db, Guid leagueId, CancellationToken cancellationToken) =>
        await db.LeagueManagers
            .Where(m => m.LeagueId == leagueId)
            .OrderBy(m => m.AssignedAt)
            .ThenBy(m => m.UserId)
            .Select(m => new LeagueManagerRow(
                m.UserId,
                m.User.Email,
                db.ClubMemberships
                    .Where(c => c.ClubId == m.League.ClubId && c.Status == ClubMembershipStatus.Active && c.Player.UserId == m.UserId)
                    .OrderBy(c => c.Player.CreatedAt)
                    .ThenBy(c => c.PlayerId)
                    .Select(c => c.Player.Name)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

    /// <summary>The same, as their Players see it: names only.</summary>
    public static async Task<IReadOnlyList<LeagueManagerName>> NamesAsync(TennisDbContext db, Guid leagueId, CancellationToken cancellationToken) =>
        [.. (await OfAsync(db, leagueId, cancellationToken)).Select(m => new LeagueManagerName(m.Name))];
}

/// <summary>A League's manager, for whoever may see who they are.</summary>
/// <param name="Name">Their Player in the League's Club; null if they have none there.</param>
public sealed record LeagueManagerRow(Guid UserId, string Email, string? Name);
