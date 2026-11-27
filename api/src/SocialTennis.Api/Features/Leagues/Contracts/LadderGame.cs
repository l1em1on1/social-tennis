namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Division's Game this Stage, between two of its Teams (#7), or in Foursomes two pairs of its Players (#14).</summary>
/// <param name="FirstTeamId">Side 1, the Team higher on the ladder; in Foursomes, the higher of Side 1's Players.</param>
/// <param name="SecondTeamId">Side 2; in Foursomes, the higher of its Players.</param>
/// <param name="Score">None until a Player who played it enters one (#38), or a Manager records it (#10).</param>
/// <param name="NotPlayed">A Manager marked it not played (#10): it counts for nothing.</param>
/// <param name="FirstPartnerTeamId">In Foursomes, Side 1's other Player (their Team of one, ADR-0036); null otherwise.</param>
/// <param name="SecondPartnerTeamId">In Foursomes, Side 2's other Player; null otherwise.</param>
/// <param name="Date">The Date its Players agreed (#9); none until one of them sets it.</param>
/// <param name="Court">Where, as its Players noted it; none if they didn't.</param>
public record LadderGame(
    Guid Id,
    Guid FirstTeamId,
    Guid SecondTeamId,
    LadderGameScore? Score,
    bool NotPlayed,
    Guid? FirstPartnerTeamId = null,
    Guid? SecondPartnerTeamId = null,
    DateOnly? Date = null,
    string? Court = null);
