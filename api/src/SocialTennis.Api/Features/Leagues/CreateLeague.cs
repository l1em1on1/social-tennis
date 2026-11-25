using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Access;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Club's manager, or someone with the League manager role there (#220), creates
/// a League for it. Its Type, Structure, Stage length,
/// roll mode and Score format are fixed here; the request's validator has
/// already refused Foursomes + Knockout and a Match Tiebreak on a one-Set Game.
/// </summary>
public static class CreateLeague
{
    public static async Task<Results<Created<CreateLeagueResponse>, NotFound>> HandleAsync(
        Guid clubId,
        CreateLeagueRequest request,
        ClaimsPrincipal user,
        ManagerAccess access,
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
        if (!await access.RunsClubAsync(user, clubId, cancellationToken))
        {
            // A League manager runs what they create (#220). A Club's manager needs no assignment, so isn't given one.
            db.LeagueManagers.Add(new LeagueManager { LeagueId = league.Id, UserId = user.UserId(), AssignedAt = league.CreatedAt });
        }

        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/leagues/{league.Id}", new CreateLeagueResponse(league.Id, league.Name));
    }
}
