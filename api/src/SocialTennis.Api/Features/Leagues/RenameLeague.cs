using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Manager changes a League's name, at any point in its life (#153). Its
/// participants, Join Requests, dates and format are untouched.
/// </summary>
public static class RenameLeague
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid leagueId,
        RenameLeagueRequest request,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var renamed = await db.Leagues
            .Where(l => l.Id == leagueId)
            .ExecuteUpdateAsync(league => league.SetProperty(l => l.Name, name), cancellationToken);
        return renamed == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }
}
