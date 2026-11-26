using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Access;
using SocialTennis.Api.Contracts;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>The Leagues of a Club the caller runs, latest start first: all of them for its managers, and their own for a League manager (#220).</summary>
public static class GetClubLeagues
{
    public static async Task<Results<Ok<GetClubLeaguesResponse>, NotFound>> HandleAsync(
        Guid clubId,
        ClaimsPrincipal user,
        TennisDbContext db,
        ManagerAccess access,
        CancellationToken cancellationToken)
    {
        if (!await db.Clubs.AnyAsync(c => c.Id == clubId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var leagues = await access.LeaguesRunIn(user, clubId)
            .OrderByDescending(l => l.StartDate)
            .ThenBy(l => l.Name)
            .Select(l => new ClubLeague(
                l.Id,
                l.Name,
                l.Type,
                l.Structure,
                l.StartDate,
                l.EndDate,
                l.StageLength,
                l.StageRoll,
                l.ScoreFormat,
                l.DivisionSize,
                l.StandingsMode,
                l.Participants.Count,
                l.JoinRequests.Count(r => r.Status == LeagueJoinRequestStatus.Pending),
                l.Stages
                    .OrderByDescending(s => s.Number)
                    .Select(s => new LeagueStageSummary(s.Number, s.StartDate, s.EndDate))
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new GetClubLeaguesResponse(leagues, new PageInfo(leagues.Count)));
    }
}
