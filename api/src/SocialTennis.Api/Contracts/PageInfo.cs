namespace SocialTennis.Api.Contracts;

/// <summary>
/// The list-level facts every list endpoint carries (ADR-0012). The one part of
/// an otherwise bespoke envelope that is identical everywhere, so it is shared
/// rather than redeclared per feature.
/// </summary>
/// <param name="Total">
/// The number of items matching the query, <em>ignoring any paging window</em>.
/// Today that always equals the returned collection's length; once paging
/// exists it will not. Fixing the meaning now means paging changes the value
/// but never the definition — a client using this as "how many did I get" is
/// wrong today rather than broken later.
/// </param>
public record PageInfo(int Total);
