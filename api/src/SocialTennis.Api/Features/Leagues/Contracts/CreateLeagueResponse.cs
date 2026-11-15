namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>The League just created. Nobody is in it yet.</summary>
public record CreateLeagueResponse(Guid Id, string Name);
