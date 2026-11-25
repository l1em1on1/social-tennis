using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Access;
using SocialTennis.Api.Contracts;
using SocialTennis.Api.Features.Clubs.Contracts;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>The Clubs the caller has a role in; every Club, for an Admin.</summary>
public static class GetClubs
{
    public static async Task<GetClubsResponse> HandleAsync(
        ClaimsPrincipal user,
        ManagerAccess access,
        CancellationToken cancellationToken)
    {
        // Project before materialising: EF translates the Select into the SELECT
        // list, so only the published columns leave Postgres and nothing is
        // change-tracked (EF Core docs, "Efficient querying" / "Tracking").
        var clubs = await access.ClubsOf(user)
            .OrderBy(c => c.Name)
            .Select(c => new ClubSummary(c.Id, c.Name))
            .ToListAsync(cancellationToken);

        // Unwindowed, so the count is the whole matching set — which is what
        // PageInfo.Total means. Once paging lands this becomes its own
        // CountAsync against the query before the window is applied.
        return new GetClubsResponse(clubs, new PageInfo(clubs.Count));
    }
}
