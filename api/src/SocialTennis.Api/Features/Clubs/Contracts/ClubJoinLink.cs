namespace SocialTennis.Api.Features.Clubs.Contracts;

/// <summary>
/// A Club's join link, as its Managers see it (#222): the code the link
/// carries, <c>/join/{code}</c> on the web app, or none while it's off.
/// </summary>
public record ClubJoinLink(string? Code);
