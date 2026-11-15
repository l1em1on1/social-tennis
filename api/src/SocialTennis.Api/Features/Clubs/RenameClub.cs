using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Clubs.Contracts;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>An Admin changes a Club's name. Its members, requests and invites are untouched.</summary>
public static class RenameClub
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid clubId,
        RenameClubRequest request,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var renamed = await db.Clubs
            .Where(c => c.Id == clubId)
            .ExecuteUpdateAsync(club => club.SetProperty(c => c.Name, name), cancellationToken);
        return renamed == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }
}
