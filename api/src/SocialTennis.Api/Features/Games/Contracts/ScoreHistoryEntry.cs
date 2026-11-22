using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Games.Contracts;

/// <summary>One change to a Game's Score (#38): who, when, from what to what.</summary>
/// <param name="By">
/// The Player who entered or corrected it. For a Manager's override, the
/// Manager's own Player, their first, or none if they hold none.
/// </param>
/// <param name="Before">None when the Score was entered.</param>
public record ScoreHistoryEntry(ScoreChangeKind Kind, string? By, DateTimeOffset At, ScoreSnapshot? Before, ScoreSnapshot After);
