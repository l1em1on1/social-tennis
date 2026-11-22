using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Contracts;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>A Club's Leagues for its Managers, latest start first.</summary>
public static class GetClubLeagues
{
    public static async Task<Results<Ok<GetClubLeaguesResponse>, NotFound>> HandleAsync(
        Guid clubId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await db.Clubs.AnyAsync(c => c.Id == clubId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var leagues = await db.Leagues
            .Where(l => l.ClubId == clubId)
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
