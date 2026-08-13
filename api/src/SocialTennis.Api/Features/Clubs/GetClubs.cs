using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Contracts;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Clubs.Contracts;

namespace SocialTennis.Api.Features.Clubs;

public static class GetClubs
{
    public static async Task<GetClubsResponse> HandleAsync(
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        // Project before materialising: EF translates the Select into the SELECT
        // list, so only the published columns leave Postgres and nothing is
        // change-tracked (EF Core docs, "Efficient querying" / "Tracking").
        var clubs = await db.Clubs
            .OrderBy(c => c.Name)
            .Select(c => new ClubSummary(c.Id, c.Name))
            .ToListAsync(cancellationToken);

        // Unwindowed, so the count is the whole matching set — which is what
        // PageInfo.Total means. Once paging lands this becomes its own
        // CountAsync against the query before the window is applied.
        return new GetClubsResponse(clubs, new PageInfo(clubs.Count));
    }
}
