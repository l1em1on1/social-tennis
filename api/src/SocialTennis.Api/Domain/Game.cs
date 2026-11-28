namespace SocialTennis.Api.Domain;

/// <summary>
/// Two Sides playing a match (see CONTEXT.md, Game): here a Division's Game in
/// a League Stage, made when the Stage opens (#7). Its <see cref="Score"/> is
/// entered once it's played (#38). Its Players agree a Date, and may note a
/// Court, as they arrange it (#9).
/// </summary>
public class Game
{
    public Guid Id { get; set; }

    public Guid LeagueId { get; set; }

    public Guid StageId { get; set; }

    public Stage Stage { get; set; } = null!;

    /// <summary>The Division it is played in, 1 at the top.</summary>
    public int Division { get; set; }

    /// <summary>Its two Sides, 1 and 2.</summary>
    public List<GameSide> Sides { get; set; } = [];

    /// <summary>None until a Player who played it enters one (#38), or a Manager records it (#10).</summary>
    public Score? Score { get; set; }

    /// <summary>
    /// When a Manager marked it not played (see CONTEXT.md, Void; #10): it
    /// counts for nothing, and takes no Score. None while it may still be
    /// played. Undone only while its Stage is open.
    /// </summary>
    public DateTimeOffset? VoidedAt { get; set; }

    /// <summary>The Manager who marked it not played.</summary>
    public Guid? VoidedByUserId { get; set; }

    /// <summary>
    /// The Date its Players agreed (#9): any of them sets it, and the last change stands
    /// (owner, 2026-11-27). None until one does.
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// When it starts on its <see cref="Date"/>, in the Club's time zone (#276). Set with the Date, and cleared
    /// with it. A Game arranged before #276 keeps its Date with none.
    /// </summary>
    public TimeOnly? StartTime { get; set; }

    /// <summary>Where, as the Players note it for themselves ("6 - astro"): free text, required nowhere (ADR-0006).</summary>
    public string? Court { get; set; }

    /// <summary>
    /// The Player who set the Date last. Deleting their account keeps the Player as "Former player" (#116);
    /// the reference is cleared only if the Player row itself goes.
    /// </summary>
    public Guid? DateSetById { get; set; }

    public Player? DateSetBy { get; set; }

    public const int CourtMaxLength = 50;
}
