using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Contracts;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Clubs.Contracts;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>
/// A Manager's view of a Club: requests waiting for approval first (oldest
/// first), then members by name, and the emails invited ahead of time.
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
                m.Player.User.Email,
                m.Status == ClubMembershipStatus.Active,
                m.RequestedAt))
            .ToListAsync(cancellationToken);

        var joinedEmails = members.Where(m => m.Approved).Select(m => m.Email).ToHashSet();
        var invites = (await db.ClubInvites
                .Where(i => i.ClubId == clubId)
                .OrderBy(i => i.Email)
                .Select(i => new { i.Email, i.CreatedAt })
                .ToListAsync(cancellationToken))
            .Select(i => new ClubInviteSummary(i.Email, joinedEmails.Contains(i.Email), i.CreatedAt))
            .ToList();

        return TypedResults.Ok(new GetClubMembersResponse(members, invites, new PageInfo(members.Count)));
    }
}
