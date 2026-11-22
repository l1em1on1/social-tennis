using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Manager creates a League for a Club. Its Type, Structure, Stage length,
/// roll mode and Score format are fixed here; the request's validator has
/// already refused Foursomes + Knockout and a Match Tiebreak on a one-Set Game.
/// </summary>
public static class CreateLeague
{
    public static async Task<Results<Created<CreateLeagueResponse>, NotFound>> HandleAsync(
        Guid clubId,
        CreateLeagueRequest request,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!await db.Clubs.AnyAsync(c => c.Id == clubId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var league = new League
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            Name = request.Name.Trim(),
            Type = request.Type,
            Structure = request.Structure,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            StageLength = request.StageLength,
            StageRoll = request.StageRoll,
            ScoreFormat = request.ScoreFormat,
            // Only a Divisional League of Teams has Divisions of a set size (ADR-0031).
            DivisionSize = request.Structure == LeagueStructure.Divisional && request.Type.TeamSize() > 0
                ? request.DivisionSize ?? League.DefaultDivisionSize
                : null,
            CreatedAt = clock.GetUtcNow(),
        };
        db.Leagues.Add(league);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/leagues/{league.Id}", new CreateLeagueResponse(league.Id, league.Name));
    }
}
