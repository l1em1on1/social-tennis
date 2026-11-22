namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Player of a Competitor on the ladder, by name only.</summary>
public record LadderPlayer(Guid PlayerId, string Name);
