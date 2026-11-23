using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Clubs.Contracts;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// The Club a join link is for (#222), for the page it opens. A link that's
/// off, was reset, or never existed is a 404 alike: the answer doesn't say which.
/// </summary>
public static class GetJoinLinkClub
{
    public static async Task<Results<Ok<GetJoinLinkClubResponse>, NotFound>> HandleAsync(
        string code,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var club = await db.Clubs
            .Where(c => c.JoinCode == code)
            .Select(c => new GetJoinLinkClubResponse(c.Id, c.Name))
            .SingleOrDefaultAsync(cancellationToken);
        return club is null ? TypedResults.NotFound() : TypedResults.Ok(club);
    }
}
