using SocialTennis.Api.Contracts;

namespace SocialTennis.Api.Features.Games.Contracts;

/// <summary>A Game's Score (#38).</summary>
/// <param name="PlayedOn">A calendar date in the Club's time zone (ADR-0016).</param>
/// <param name="Winner">The Side that won, 1 or 2: from the Sets, or as declared for a Retirement.</param>
/// <param name="Retired">The Game ended early: the Sets are as they stood.</param>
/// <param name="SubmittedBy">
/// The Player who entered it. When a Manager recorded it (#10), their own
/// first Player, or none if they hold none.
/// </param>
/// <param name="RecordedByManager">A Manager recorded it, for a Game nobody had scored (#10).</param>
/// <param name="Version">What a correction or an override sends back, so a stale one is refused (ADR-0018).</param>
public record GameScore(
    DateOnly PlayedOn,
    IReadOnlyList<SetScore> Sets,
    int Winner,
    bool Retired,
    string? SubmittedBy,
    bool RecordedByManager,
    DateTimeOffset SubmittedAt,
    uint Version);
