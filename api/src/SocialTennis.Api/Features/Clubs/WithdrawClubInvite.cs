using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// A Manager takes back an invite. Players already let in by it stay members:
/// removing someone is <see cref="RemoveClubMember"/>. From now on that email's
/// Players have to ask like anyone else. Withdrawing one already gone is fine.
/// </summary>
public static class WithdrawClubInvite
{
    public static async Task<NoContent> HandleAsync(
        Guid clubId,
        Guid inviteId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        // One statement, so no lock: a request racing it sees the invite or
        // doesn't, and either answer was true at that moment.
        await db.ClubInvites
            .Where(i => i.ClubId == clubId && i.Id == inviteId)
            .ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
