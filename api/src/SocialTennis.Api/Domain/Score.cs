namespace SocialTennis.Api.Domain;

/// <summary>
/// A League Game's recorded result (see CONTEXT.md, Score): the Date played,
/// its Sets, the winning Side, and who entered it. A Game has at most one,
/// keyed by the Game. Its changes are kept as <see cref="ScoreChange"/>s.
/// </summary>
public class Score
{
    public Guid GameId { get; set; }

    /// <summary>With <see cref="GameId"/>, keeps the Score in its Game's League.</summary>
    public Guid LeagueId { get; set; }

    public Game Game { get; set; } = null!;

    /// <summary>A calendar date in the Club's time zone (ADR-0016).</summary>
    public DateOnly PlayedOn { get; set; }

    /// <summary>In the order played. Stored as one JSON column.</summary>
    public List<ScoreSet> Sets { get; set; } = [];

    /// <summary>
    /// The Side that won, 1 or 2: from the Sets, or for a Retirement as
    /// declared. Stored, so Standings (#10) needn't recompute it.
    /// </summary>
    public int Winner { get; set; }

    /// <summary>The Game ended early (see CONTEXT.md, Retirement): a played result, with a declared winner.</summary>
    public bool Retired { get; set; }

    /// <summary>The Player who entered it: one who played the Game.</summary>
    public Guid SubmittedById { get; set; }

    public Player SubmittedBy { get; set; } = null!;

    public DateTimeOffset SubmittedAt { get; set; }
}
