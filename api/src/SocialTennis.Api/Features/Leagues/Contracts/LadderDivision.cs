namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>One Division of a Stage's ladder (#7), with its Game.</summary>
/// <param name="Number">1 at the top.</param>
/// <param name="Competitors">Its Competitors, the higher place first.</param>
/// <param name="GameId">The Division's Game this Stage. Its Date and Score come later (#9, #38).</param>
public record LadderDivision(int Number, IReadOnlyList<LadderCompetitor> Competitors, Guid GameId);
