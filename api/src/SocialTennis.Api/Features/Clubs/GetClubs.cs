using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Access;
using SocialTennis.Api.Contracts;
using SocialTennis.Api.Features.Clubs.Contracts;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>The Clubs the caller has any standing in, and what they may do in each; every Club, for an Admin (ADR-0034).</summary>
public static class GetClubs
{
    public static async Task<GetClubsResponse> HandleAsync(
        ClaimsPrincipal user,
        ManagerAccess access,
        CancellationToken cancellationToken)
    {
        var clubs = (await access.ClubsAsync(user, cancellationToken))
            .Select(c => new ClubSummary(c.ClubId, c.Name, c.CanManage, c.CanCreateLeagues))
            .ToList();

        // Unwindowed, so the count is the whole matching set — which is what
        // PageInfo.Total means. Once paging lands this becomes its own
        // CountAsync against the query before the window is applied.
        return new GetClubsResponse(clubs, new PageInfo(clubs.Count));
    }
}
