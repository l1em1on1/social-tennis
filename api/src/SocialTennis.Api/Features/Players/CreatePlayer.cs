using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Players.Contracts;
using SocialTennis.Api.Membership;

namespace SocialTennis.Api.Features.Players;

/// <summary>
/// The signed-in User adds a Player, which they then hold. Refused (409) once
/// the login holds <see cref="Player.MaxPerUser"/> Players (#109): enough for a
/// family, and a ceiling on how many join requests one login can put in front of
/// a Club's Managers. Every Player the login holds counts, including ones a
/// Manager added for its email; a Manager adding a Player (AddClubPlayer) is
/// never refused, so a Manager can still add more.
/// </summary>
public static class CreatePlayer
{
    public static async Task<Results<Created<CreatePlayerResponse>, ProblemHttpResult>> HandleAsync(
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
            Gender = request.Gender,
            Level = request.Level,
            UserId = user.UserId(),
            CreatedAt = clock.GetUtcNow(),
        };

        // A Manager may have invited this email already: then the new Player
        // is in those Clubs from the start, with nothing to ask for. Locked, so
        // an invite made at this moment can't miss the new Player.
        var email = EmailAddress.Normalize(user.FindFirstValue(ClaimTypes.Email)!);
        await using var transaction = await ClubAdmission.LockEmailAsync(db, email, cancellationToken);

        // Counted under the same per-email lock, so two adds at once can't both
        // slip in under the limit.
        if (await db.Players.CountAsync(p => p.UserId == player.UserId, cancellationToken) >= Player.MaxPerUser)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "player-limit",
                title: $"A login can hold at most {Player.MaxPerUser} Players.",
                detail: "Ask a Manager to add any more.");
        }

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
