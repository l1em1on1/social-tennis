using SocialTennis.Api.Contracts;

namespace SocialTennis.Api.Features.Clubs.Contracts;

/// <summary>
/// The envelope for <see cref="GetClubMembers"/> (ADR-0012). <c>Page</c> counts
/// <c>Members</c>; the invited emails ride along because a Manager reads the
/// two together.
/// </summary>
public record GetClubMembersResponse(
    IReadOnlyList<ClubMember> Members,
    IReadOnlyList<ClubInviteSummary> Invites,
    PageInfo Page);
