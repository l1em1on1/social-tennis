namespace SocialTennis.Api.Features.Clubs.Contracts;

/// <summary>
/// The public shape of a Club. Deliberately separate from the
/// <see cref="SocialTennis.Api.Domain.Club"/> entity so that adding a column — an internal or
/// audit field, say — is not automatically a public API change.
/// </summary>
public record ClubResponse(Guid Id, string Name);
