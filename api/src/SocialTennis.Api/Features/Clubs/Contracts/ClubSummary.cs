namespace SocialTennis.Api.Features.Clubs.Contracts;

/// <summary>
/// One Club as it appears in a list. Deliberately separate from the
/// <see cref="SocialTennis.Api.Domain.Club"/> entity, so that adding a column —
/// an internal or audit field, say — is not automatically a public API change.
/// </summary>
/// <remarks>
/// A summary, not a detail view: when a single-Club endpoint arrives it gets its
/// own independent record rather than reusing this one (ADR-0012). Enriching the
/// detail view must never silently widen every row of a list.
/// </remarks>
public record ClubSummary(Guid Id, string Name);
