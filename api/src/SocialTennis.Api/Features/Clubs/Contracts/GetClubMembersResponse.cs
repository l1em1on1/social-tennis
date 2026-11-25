using SocialTennis.Api.Contracts;

namespace SocialTennis.Api.Features.Clubs.Contracts;

/// <summary>
/// The envelope for <see cref="GetClubMembers"/> (ADR-0012). <c>Page</c> counts
/// <c>Members</c>; the invited emails and the Club's roles (#220) ride along
/// because a Manager reads them together.
/// </summary>
public record GetClubMembersResponse(
    IReadOnlyList<ClubMember> Members,
    IReadOnlyList<ClubInviteSummary> Invites,
    IReadOnlyList<ClubRoleSummary> Roles,
    PageInfo Page);
