namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>One Division of a Stage's ladder (#7): its Teams, their Games (ADR-0031), and its Standings (#10).</summary>
/// <param name="Number">1 at the top.</param>
/// <param name="Competitors">Its Teams, the higher place first.</param>
/// <param name="Games">Every pair of them once this Stage, in ladder order, each with its Score once entered (#38).</param>
/// <param name="Standings">Every Team, first first; Teams still tied in ladder order, or in a Foursomes tie the Players marked, theirs.</param>
/// <param name="PlayersTies">Foursomes only (#14 part 3): the ties its Players settle, marked or not. Empty otherwise.</param>
public record LadderDivision(
    int Number,
    IReadOnlyList<LadderCompetitor> Competitors,
    IReadOnlyList<LadderGame> Games,
    IReadOnlyList<LadderStanding> Standings,
    IReadOnlyList<LadderPlayersTie> PlayersTies);
