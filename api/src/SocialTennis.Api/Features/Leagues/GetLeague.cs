using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>A League as a Manager runs it: format, participants, Join Requests and Teams.</summary>
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
                l.DivisionSize,
                l.Participants
                    .OrderBy(p => p.JoinedAt)
                    .ThenBy(p => p.Player.Name)
                    .Select(p => new LeagueParticipantSummary(
                        p.PlayerId, p.Player.Name, p.Player.User != null ? p.Player.User.Email : null, p.Player.Rating, p.JoinedAt, p.Player.Gender, p.TeamId))
                    .ToList(),
                l.JoinRequests
                    .OrderBy(r => r.Status != LeagueJoinRequestStatus.Pending)
                    .ThenBy(r => r.Status == LeagueJoinRequestStatus.Pending ? r.RequestedAt : DateTimeOffset.MinValue)
                    .ThenByDescending(r => r.DecidedAt)
                    .Select(r => new LeagueJoinRequestSummary(
                        r.PlayerId,
                        r.Player.Name,
                        // A Former player's own requests are deleted with their User (#116).
                        r.Player.User!.Email,
                        r.Player.Rating,
                        r.Player.Gender,
                        r.Partner == null
                            ? null
                            : new LeagueJoinRequestPartner(
                                r.Partner.Id, r.Partner.Name, r.Partner.User != null ? r.Partner.User.Email : null, r.Partner.Rating, r.Partner.Gender),
                        r.Status,
                        r.RequestedAt,
                        r.DecidedAt))
                    .ToList(),
                l.Teams
                    .OrderBy(t => t.CreatedAt)
                    .ThenBy(t => t.Id)
                    .Select(t => new LeagueTeamSummary(
                        t.Id,
                        t.Members.OrderBy(m => m.JoinedAt).ThenBy(m => m.PlayerId).Select(m => m.PlayerId).ToList(),
                        t.Members.Average(m => (double)m.Player.Rating),
                        null,
                        null))
                    .ToList(),
                l.Stages
                    .OrderByDescending(s => s.Number)
                    .Select(s => new LeagueStageSummary(s.Number, s.StartDate, s.EndDate))
                    .FirstOrDefault()))
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);
        if (league is null)
        {
            return TypedResults.NotFound();
        }

        if (league.Stage is null)
        {
            return TypedResults.Ok(league);
        }

        // Each Team's place on the current Stage's ladder (#7).
        var places = await db.LadderPlaces
            .Where(p => p.LeagueId == leagueId && p.Stage.Number == league.Stage.Number)
            .ToDictionaryAsync(p => p.TeamId, p => (p.Position, p.Division), cancellationToken);
        return TypedResults.Ok(league with
        {
            Teams =
            [
                .. league.Teams.Select(team => places.TryGetValue(team.Id, out var place)
                    ? team with { Position = place.Position, Division = place.Division }
                    : team),
            ],
        });
    }
}
