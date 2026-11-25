using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Membership;

/// <summary>Who plays in a Club, for the roles only someone in it may be given (#220).</summary>
internal static class ClubMembers
{
    /// <summary>Whether this User holds a Player in the Club: approved, not just asking.</summary>
    public static Task<bool> HasPlayerAsync(TennisDbContext db, Guid clubId, Guid userId, CancellationToken cancellationToken) =>
        db.ClubMemberships.AnyAsync(
            m => m.ClubId == clubId && m.Status == ClubMembershipStatus.Active && m.Player.UserId == userId, cancellationToken);

    public static ProblemHttpResult NotInClub() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "no-player-in-club",
            title: "They have no player in this Club, so they can't be its League manager.");
}
