using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>A League as a Manager runs it: format, participants, and Join Requests.</summary>
public static class GetLeague
{
    public static async Task<Results<Ok<GetLeagueResponse>, NotFound>> HandleAsync(
        Guid leagueId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var league = await db.Leagues
            .Where(l => l.Id == leagueId)
            .Select(l => new GetLeagueResponse(
                l.Id,
                l.ClubId,
                l.Club.Name,
                l.Name,
                l.Type,
                l.Structure,
                l.StartDate,
                l.EndDate,
                l.StageLength,
                l.StageRoll,
                l.ScoreFormat,
                l.Participants
                    .OrderBy(p => p.JoinedAt)
                    .ThenBy(p => p.Player.Name)
                    .Select(p => new LeagueParticipantSummary(
                        p.PlayerId, p.Player.Name, p.Player.User.Email, p.Player.Rating, p.JoinedAt))
                    .ToList(),
                l.JoinRequests
                    .OrderBy(r => r.Status != LeagueJoinRequestStatus.Pending)
                    .ThenBy(r => r.Status == LeagueJoinRequestStatus.Pending ? r.RequestedAt : DateTimeOffset.MinValue)
                    .ThenByDescending(r => r.DecidedAt)
                    .Select(r => new LeagueJoinRequestSummary(
                        r.PlayerId, r.Player.Name, r.Player.User.Email, r.Player.Rating, r.Status, r.RequestedAt, r.DecidedAt))
                    .ToList()))
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

        return league is null ? TypedResults.NotFound() : TypedResults.Ok(league);
    }
}
