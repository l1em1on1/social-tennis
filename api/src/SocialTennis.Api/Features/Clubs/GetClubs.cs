using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Clubs.Contracts;

namespace SocialTennis.Api.Features.Clubs;

public static class GetClubs
{
    // Project before materialising: EF translates the Select into the SELECT
    // list, so only the published columns leave Postgres and nothing is
    // change-tracked (EF Core docs, "Efficient querying" / "Tracking").
    public static async Task<List<ClubResponse>> HandleAsync(
        TennisDbContext db,
        CancellationToken cancellationToken) =>
        await db.Clubs
            .OrderBy(c => c.Name)
            .Select(c => new ClubResponse(c.Id, c.Name))
            .ToListAsync(cancellationToken);
}
