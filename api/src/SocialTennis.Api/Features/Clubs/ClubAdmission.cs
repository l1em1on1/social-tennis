using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// The one rule for who is in a Club without asking: Players held by a User
/// whose email a Manager has invited. Shared by the three places it applies:
/// inviting, asking to join, and adding a Player.
/// </summary>
internal static class ClubAdmission
{
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>Ids of the Clubs that have invited this email.</summary>
    public static Task<List<Guid>> InvitingClubIdsAsync(
        TennisDbContext db, string normalizedEmail, CancellationToken cancellationToken) =>
        db.ClubInvites
            .Where(i => i.Email == normalizedEmail)
            .Select(i => i.ClubId)
            .ToListAsync(cancellationToken);

    public static ClubMembership Membership(Guid clubId, Guid playerId, ClubMembershipStatus status, DateTimeOffset now) =>
        new() { ClubId = clubId, PlayerId = playerId, Status = status, RequestedAt = now };
}
