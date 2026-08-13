using SocialTennis.Api.Contracts;

namespace SocialTennis.Api.Features.Clubs.Contracts;

/// <summary>
/// The envelope for <see cref="GetClubs"/>. List endpoints never return a bare
/// array (ADR-0012): the envelope is where list-level facts live, so adding one
/// is additive rather than a breaking wire change.
/// </summary>
public record GetClubsResponse(IReadOnlyList<ClubSummary> Clubs, PageInfo Page);
