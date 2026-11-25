using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Access;
using SocialTennis.Api.Contracts;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// The Club members a League's manager could add to it (#220), by name: every
/// member who isn't in it. A League manager sees names alone, and a manager of
/// the Club their emails too, since two members can share a name.
/// </summary>
public static class GetAddablePlayers
{
    public static async Task<Results<Ok<GetAddablePlayersResponse>, NotFound>> HandleAsync(
        Guid leagueId,
        ClaimsPrincipal user,
        TennisDbContext db,
        ManagerAccess access,
        CancellationToken cancellationToken)
    {
        var clubId = await db.Leagues.Where(l => l.Id == leagueId).Select(l => (Guid?)l.ClubId).SingleOrDefaultAsync(cancellationToken);
        if (clubId is null)
        {
            return TypedResults.NotFound();
        }

        var emails = await access.RunsClubAsync(user, clubId.Value, cancellationToken);
        var players = await db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.Status == ClubMembershipStatus.Active
                && !db.LeagueParticipants.Any(p => p.LeagueId == leagueId && p.PlayerId == m.PlayerId))
            .OrderBy(m => m.Player.Name)
            .ThenBy(m => m.PlayerId)
            .Select(m => new AddablePlayer(m.PlayerId, m.Player.Name, emails ? m.Player.User!.Email : null))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new GetAddablePlayersResponse(players, new PageInfo(players.Count)));
    }
}
