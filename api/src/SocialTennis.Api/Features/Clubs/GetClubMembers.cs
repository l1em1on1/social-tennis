using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Contracts;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Clubs.Contracts;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// A Manager's view of a Club: requests waiting for approval first (oldest
/// first), then members by name, and the emails invited ahead of time that
/// haven't let anyone in yet.
/// </summary>
public static class GetClubMembers
{
    public static async Task<Results<Ok<GetClubMembersResponse>, NotFound>> HandleAsync(
        Guid clubId,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await db.Clubs.AnyAsync(c => c.Id == clubId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var members = await db.ClubMemberships
            .Where(m => m.ClubId == clubId)
            .OrderBy(m => m.Status)
            .ThenBy(m => m.Status == ClubMembershipStatus.Pending ? m.RequestedAt : DateTimeOffset.MinValue)
            .ThenBy(m => m.Player.Name)
            .Select(m => new ClubMember(
                m.PlayerId,
                m.Player.Name,
                // Held by a User: a Former player (#116) is a member of no Club.
                m.Player.User!.Email,
                // The sessions are for a User who hasn't signed in since LastSignedInAt was added (#117):
                // on their own they forget, as the nightly purge deletes ended ones.
                m.Player.User!.LastSignedInAt != null || db.SessionTokens.Any(s => s.UserId == m.Player.UserId),
                m.Status == ClubMembershipStatus.Active,
                m.Player.Gender,
                m.Player.Level,
                m.Player.Rating,
                m.RequestedAt))
            .ToListAsync(cancellationToken);

        var invites = await db.ClubInvites
            .Where(i => i.ClubId == clubId)
            .OrderBy(i => i.Email)
            .Select(i => new ClubInviteSummary(i.Id, i.Email, i.CreatedAt))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new GetClubMembersResponse(members, invites, new PageInfo(members.Count)));
    }
}
