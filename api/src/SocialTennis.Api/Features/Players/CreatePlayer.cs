using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.Features.Players;

/// <summary>The signed-in User adds a Player, which they then hold.</summary>
public static class CreatePlayer
{
    public static async Task<Created<CreatePlayerResponse>> HandleAsync(
        CreatePlayerRequest request,
        ClaimsPrincipal user,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var player = new Player
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            UserId = user.UserId(),
            CreatedAt = clock.GetUtcNow(),
        };
        db.Players.Add(player);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/players/{player.Id}", new CreatePlayerResponse(player.Id, player.Name));
    }
}
