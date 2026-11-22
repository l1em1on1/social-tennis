namespace SocialTennis.Api.Domain;

/// <summary>
/// A time-boxed period of a League in which its Games are arranged and played
/// (see CONTEXT.md, Stage). Starting a League opens Stage 1 (#7); a Stage's
/// ladder is its <see cref="Places"/>, and its Divisions are read from them.
/// </summary>
public class Stage
{
    public Guid Id { get; set; }

    public Guid LeagueId { get; set; }

    public League League { get; set; } = null!;

    /// <summary>1 for the first Stage, counting up as Stages roll (#10).</summary>
    public int Number { get; set; }

    /// <summary>A calendar date in the Club's time zone (ADR-0016).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>A calendar date in the Club's time zone; the deadline passes at the end of it (ADR-0016).</summary>
    public DateOnly EndDate { get; set; }

    public DateTimeOffset OpenedAt { get; set; }

    /// <summary>
    /// When the Stage closed (#10); none while it is open. Players' Scores are
    /// editable until then, whatever the date: the lock follows this, never
    /// <see cref="EndDate"/> (ADR-0016). A Manager can still override after.
    /// </summary>
    public DateTimeOffset? ClosedAt { get; set; }

    public List<LadderPlace> Places { get; set; } = [];

    public List<Game> Games { get; set; } = [];
}
