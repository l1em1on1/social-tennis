namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>The Players' answer to a tie (#14 part 3): who goes up, or down.</summary>
/// <param name="TeamId">The one who moves.</param>
/// <param name="MarkedBy">The Player who said so; null if they have since been deleted.</param>
public record LadderTieMark(Guid TeamId, string? MarkedBy, DateTimeOffset MarkedAt);
