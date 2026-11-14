using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Contracts;
using SocialTennis.Api.Data;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.Features.Players;

/// <summary>The Players the signed-in User holds, oldest first: the first one made is the default Acting Player.</summary>
public static class GetPlayers
{
    public static async Task<GetPlayersResponse> HandleAsync(
        ClaimsPrincipal user,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        var players = await db.Players
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Select(p => new PlayerSummary(
                p.Id,
                p.Name,
                p.Memberships
                    .OrderBy(m => m.Club.Name)
                    .Select(m => new PlayerClub(m.ClubId, m.Club.Name))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return new GetPlayersResponse(players, new PageInfo(players.Count));
    }
}
