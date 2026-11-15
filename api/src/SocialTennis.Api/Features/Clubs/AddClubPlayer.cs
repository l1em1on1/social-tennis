using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Membership;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// A Manager adds a Player to the Club by name and email, whether or not that
/// person has ever signed in. This is how a League is filled before its
/// players use the app (issue #4, ADR-0008 phasing). The Player is a member at
/// once, and is held by whoever signs in with that email: all of them, if a
/// parent was given several children's Players under one address.
/// </summary>
/// <remarks>
/// Unlike an invite it makes the Player now, so it can be put in a League
/// before the person arrives. Adding the same name and email twice makes two
/// Players: two children can share a name, and a Manager can remove a mistake.
/// </remarks>
public static class AddClubPlayer
{
    public static async Task<Results<Created<AddClubPlayerResponse>, NotFound>> HandleAsync(
        Guid clubId,
        AddClubPlayerRequest request,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!await db.Clubs.AnyAsync(c => c.Id == clubId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var email = EmailAddress.Normalize(request.Email);
        var now = clock.GetUtcNow();

        await using var transaction = await ClubAdmission.LockEmailAsync(db, email, cancellationToken);
        var player = new Player
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            UserId = await UserAccounts.EnsureAsync(db, email, now, cancellationToken),
            CreatedAt = now,
        };
        db.Players.Add(player);
        db.ClubMemberships.Add(ClubAdmission.Membership(clubId, player.Id, ClubMembershipStatus.Active, now));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.Created((string?)null, new AddClubPlayerResponse(player.Id, player.Name));
    }
}
