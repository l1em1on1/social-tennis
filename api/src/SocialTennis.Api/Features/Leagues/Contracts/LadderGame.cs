namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Division's Game this Stage, between two of its Teams (#7).</summary>
/// <param name="FirstTeamId">Side 1, the Team higher on the ladder.</param>
/// <param name="SecondTeamId">Side 2.</param>
public record LadderGame(Guid Id, Guid FirstTeamId, Guid SecondTeamId);
