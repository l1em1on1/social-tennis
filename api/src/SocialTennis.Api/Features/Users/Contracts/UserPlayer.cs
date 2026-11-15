namespace SocialTennis.Api.Features.Users.Contracts;

/// <summary>A Player a User holds, as an Admin sees it: just enough to recognise the person.</summary>
public record UserPlayer(Guid Id, string Name);
