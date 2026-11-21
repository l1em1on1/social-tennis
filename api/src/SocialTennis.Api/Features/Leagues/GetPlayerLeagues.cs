using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Contracts;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// The Leagues at Clubs one of the signed-in User's Players is a member of: the
/// ones it is in and the ones it could join, with its own Join Request on each,
/// and a waiting request that names it as a partner (#6). Once a League has
/// started, its current Stage and the Player's place on the ladder (#7).
/// Latest start first. Another User's Player is a 404. A Player removed from a
/// Club, or who left it, no longer sees that Club's Leagues (#109); leaving a
/// League properly is #21.
/// </summary>
public static class GetPlayerLeagues
{
    public static async Task<Results<Ok<GetPlayerLeaguesResponse>, NotFound>> HandleAsync(
        Guid playerId,
        ClaimsPrincipal user,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        if (!await db.Players.AnyAsync(p => p.Id == playerId && p.UserId == userId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var leagues = await db.Leagues
            .Where(l => db.ClubMemberships.Any(m =>
                m.ClubId == l.ClubId && m.PlayerId == playerId && m.Status == ClubMembershipStatus.Active))
            .OrderByDescending(l => l.StartDate)
            .ThenBy(l => l.Name)
            .Select(l => new PlayerLeague(
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
                l.Participants.Count,
                l.Participants.Any(p => p.PlayerId == playerId),
                l.JoinRequests
                    .Where(r => r.PlayerId == playerId)
                    .Select(r => new PlayerLeagueJoinRequest(
                        r.Status, r.RequestedAt, r.DecidedAt, r.Partner != null ? r.Partner.Name : null))
                    .SingleOrDefault(),
                l.Participants
                    .Where(p => p.PlayerId == playerId && p.TeamId != null)
                    .SelectMany(p => p.Team!.Members)
                    .Where(m => m.PlayerId != playerId)
                    .Select(m => m.Player.Name)
                    .FirstOrDefault(),
                // Waiting only: once rejected, the request no longer concerns
                // the partner, who is free to ask. At most one, as asking
                // enforces; FirstOrDefault so a second can't break the page.
                l.JoinRequests
                    .Where(r => r.PartnerId == playerId && r.Status == LeagueJoinRequestStatus.Pending)
                    .Select(r => new PlayerLeaguePartnerRequest(r.PlayerId, r.Player.Name, r.RequestedAt))
                    .FirstOrDefault(),
                l.Stages
                    .OrderByDescending(s => s.Number)
                    .Select(s => new LeagueStageSummary(s.Number, s.StartDate, s.EndDate))
                    .FirstOrDefault(),
                null))
            .ToListAsync(cancellationToken);

        // Where the Player is on each started League's ladder (#7): their
        // Division, and the other Teams in it, whom they play once each this
        // Stage (ADR-0031). Nothing if they aren't placed.
        var started = leagues.Where(l => l.Joined && l.Stage is not null).Select(l => l.Id).ToList();
        if (started.Count > 0)
        {
            var mine = await db.LadderPlaces
                .Where(p => started.Contains(p.LeagueId)
                    && !db.Stages.Any(n => n.LeagueId == p.LeagueId && n.Number > p.Stage.Number)
                    && p.Team.Members.Any(m => m.PlayerId == playerId))
                .Select(p => new
                {
                    p.LeagueId,
                    p.Division,
                    Opponents = p.Stage.Places
                        .Where(other => other.Division == p.Division && other.TeamId != p.TeamId)
                        .OrderBy(other => other.Position)
                        .Select(other => new PlayerLeagueOpponent(
                            other.TeamId,
                            other.Team.Members.OrderBy(m => m.JoinedAt).ThenBy(m => m.PlayerId).Select(m => m.Player.Name).ToList()))
                        .ToList(),
                })
                .ToListAsync(cancellationToken);
            var places = mine.ToDictionary(p => p.LeagueId);
            leagues =
            [
                .. leagues.Select(league => places.TryGetValue(league.Id, out var place)
                    ? league with { Place = new PlayerLeaguePlace(place.Division, place.Opponents) }
                    : league),
            ];
        }

        return TypedResults.Ok(new GetPlayerLeaguesResponse(leagues, new PageInfo(leagues.Count)));
    }
}
