using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Players.Contracts;
using SocialTennis.Api.Membership;

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

        // A Manager may have invited this email already: then the new Player
        // is in those Clubs from the start, with nothing to ask for. Locked, so
        // an invite made at this moment can't miss the new Player.
        var email = EmailAddress.Normalize(user.FindFirstValue(ClaimTypes.Email)!);
        await using var transaction = await ClubAdmission.LockEmailAsync(db, email, cancellationToken);

        db.Players.Add(player);
        foreach (var clubId in await ClubAdmission.InvitingClubIdsAsync(db, email, cancellationToken))
        {
            db.ClubMemberships.Add(
                ClubAdmission.Membership(clubId, player.Id, ClubMembershipStatus.Active, player.CreatedAt));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // No Location: there is no route that reads a single Player. GET
        // /players lists them all, and that is where the new one appears.
        return TypedResults.Created((string?)null, new CreatePlayerResponse(player.Id, player.Name));
    }
}
