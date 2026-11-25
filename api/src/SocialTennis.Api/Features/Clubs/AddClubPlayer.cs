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
/// parent was given several children's Players under one address. Like a
/// Player the User adds, it also joins the Clubs that invited the email or that
/// the User is already in, and uses those invites up (#164).
/// </summary>
/// <remarks>
/// Unlike an invite it makes the Player now, so it can be put in a League
/// before the person arrives. It also lets in the Players that email's User
/// already holds, as pre-approving the email does: on Manage the two are one
/// form, "Add someone", whose name is optional (#224). A name that User already
/// holds (any case) makes no second Player: that Player is let in and returned,
/// with 200 rather than 201.
/// </remarks>
public static class AddClubPlayer
{
    public static async Task<Results<Created<AddClubPlayerResponse>, Ok<AddClubPlayerResponse>, NotFound>> HandleAsync(
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

        var name = request.Name.Trim();

        await using var transaction = await ClubAdmission.LockEmailAsync(db, email, cancellationToken);
        var userId = await UserAccounts.EnsureAsync(db, email, now, cancellationToken);
        // The login's Players come in with it, the one named too if it holds that name already.
        await ClubAdmission.LetInAsync(db, email, clubId, now, cancellationToken);
        var held = await db.Players
            .Where(p => p.UserId == userId)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken);
        if (held.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) is { } existing)
        {
            await ClubAdmission.UseUpInvitesAsync(db, email, cancellationToken, clubId);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return TypedResults.Ok(new AddClubPlayerResponse(existing.Id, existing.Name));
        }

        var player = new Player
        {
            Id = Guid.NewGuid(),
            Name = name,
            UserId = userId,
            CreatedAt = now,
        };
        db.Players.Add(player);
        db.ClubMemberships.Add(ClubAdmission.Membership(clubId, player.Id, ClubMembershipStatus.Active, now));
        await ClubAdmission.AdmitNewPlayerAsync(db, userId, email, player.Id, now, cancellationToken, addedByUser: false, except: clubId);
        // The User is in this Club now: a request of theirs waiting here is approved.
        await ClubAdmission.ApproveWaitingAsync(db, userId, clubId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.Created((string?)null, new AddClubPlayerResponse(player.Id, player.Name));
    }
}
