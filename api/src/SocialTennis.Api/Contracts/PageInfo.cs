namespace SocialTennis.Api.Contracts;

/// <summary>
/// The list-level facts every list endpoint carries (ADR-0012). The one part of
/// an otherwise bespoke envelope that is identical everywhere, so it is shared
/// rather than redeclared per feature.
/// </summary>
/// <param name="Total">
/// The number of items matching the query, <em>ignoring any paging window</em>.
/// For a paged list it is more than the returned collection's length whenever
/// there are further pages; for an unpaged one the two are equal.
/// </param>
/// <param name="Offset">How many matching items come before this window: 0 for an unpaged list.</param>
/// <param name="Limit">
/// The most items this window can hold, or <c>null</c> when the list isn't
/// paged and holds every match. Paging arrived with <c>GET /users</c> (#134);
/// every envelope carries both fields so a list that becomes paged later
/// changes their values, never the shape (ADR-0012).
/// </param>
public record PageInfo(int Total, int Offset = 0, int? Limit = null);
