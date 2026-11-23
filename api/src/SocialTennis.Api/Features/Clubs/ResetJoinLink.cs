using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Membership;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// A Manager resets the Club's join link (#222): a new code, and the old link
/// stops working, say once it has reached people it shouldn't. Requests
/// already made through the old link stay, for a Manager to decide. A link
/// that's off is turned on.
/// </summary>
public static class ResetJoinLink
{
    public static async Task<Results<Ok<ClubJoinLink>, NotFound>> HandleAsync(
        Guid clubId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var code = JoinCodes.New();
        var updated = await db.Clubs
            .Where(c => c.Id == clubId)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.JoinCode, code), cancellationToken);
        return updated == 0 ? TypedResults.NotFound() : TypedResults.Ok(new ClubJoinLink(code));
    }
}
