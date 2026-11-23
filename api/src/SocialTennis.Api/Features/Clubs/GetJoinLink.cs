using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Clubs.Contracts;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>A Manager reads the Club's join link (#222): its code, or none while it's off.</summary>
public static class GetJoinLink
{
    public static async Task<Results<Ok<ClubJoinLink>, NotFound>> HandleAsync(
        Guid clubId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var club = await db.Clubs
            .Where(c => c.Id == clubId)
            .Select(c => new { c.JoinCode })
            .SingleOrDefaultAsync(cancellationToken);
        return club is null ? TypedResults.NotFound() : TypedResults.Ok(new ClubJoinLink(club.JoinCode));
    }
}
