using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Membership;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// A Manager turns the Club's join link on (#222): a new code if it was off,
/// and the same one if it was on, so turning it on twice, or two Managers at
/// once, give one link that works. Resetting it is <see cref="ResetJoinLink"/>.
/// </summary>
public static class TurnOnJoinLink
{
    public static async Task<Results<Ok<ClubJoinLink>, NotFound>> HandleAsync(
        Guid clubId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        // Conditional, so a second turn-on waits on the first's row lock, then finds a code and keeps it.
        var code = JoinCodes.New();
        await db.Clubs
            .Where(c => c.Id == clubId && c.JoinCode == null)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.JoinCode, code), cancellationToken);
        var club = await db.Clubs
            .Where(c => c.Id == clubId)
            .Select(c => new { c.JoinCode })
            .SingleOrDefaultAsync(cancellationToken);
        return club is null ? TypedResults.NotFound() : TypedResults.Ok(new ClubJoinLink(club.JoinCode));
    }
}
