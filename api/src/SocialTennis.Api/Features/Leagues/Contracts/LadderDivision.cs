namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>One Division of a Stage's ladder (#7): its Teams and their Games (ADR-0031).</summary>
/// <param name="Number">1 at the top.</param>
/// <param name="Competitors">Its Teams, the higher place first.</param>
/// <param name="Games">Every pair of them once this Stage, in ladder order. A Game's Date and Score come later (#9, #38).</param>
public record LadderDivision(int Number, IReadOnlyList<LadderCompetitor> Competitors, IReadOnlyList<LadderGame> Games);
