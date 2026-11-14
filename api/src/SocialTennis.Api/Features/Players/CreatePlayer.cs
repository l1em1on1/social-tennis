using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Clubs;
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

        // A Manager may have invited this email already: then the new Player
        // is in those Clubs from the start, with nothing to ask for.
        var email = ClubAdmission.NormalizeEmail(user.FindFirstValue(ClaimTypes.Email)!);
        foreach (var clubId in await ClubAdmission.InvitingClubIdsAsync(db, email, cancellationToken))
        {
            db.ClubMemberships.Add(
                ClubAdmission.Membership(clubId, player.Id, ClubMembershipStatus.Active, player.CreatedAt));
        }

        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/players/{player.Id}", new CreatePlayerResponse(player.Id, player.Name));
    }
}
