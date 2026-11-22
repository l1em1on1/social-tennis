namespace SocialTennis.Api.Domain;

/// <summary>
/// A Club competition: a League Type (who plays) with a League Structure (how
/// competition is organized), run over a set duration as a sequence of Stages
/// (see CONTEXT.md). Players get in by a <see cref="LeagueJoinRequest"/> a
/// Manager approves, or by a Manager adding them; either way they become a
/// <see cref="LeagueParticipant"/>.
/// </summary>
public class League
{
    public const int NameMaxLength = 200;

    /// <summary>Teams per Division when the Manager doesn't say (owner, 2026-11-21; ADR-0031).</summary>
    public const int DefaultDivisionSize = 4;

    public const int MinDivisionSize = 2;

    public const int MaxDivisionSize = 8;

    public Guid Id { get; set; }

    public Guid ClubId { get; set; }

    public Club Club { get; set; } = null!;

    public required string Name { get; set; }

    public LeagueType Type { get; set; }

    public LeagueStructure Structure { get; set; }

    /// <summary>A calendar date in the Club's time zone (ADR-0016).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>A calendar date in the Club's time zone; the League runs to the end of it (ADR-0016).</summary>
    public DateOnly EndDate { get; set; }

    public required StageLength StageLength { get; set; }

    /// <summary>Set for a Divisional League, and only for one: a Knockout round has no Relegation to run.</summary>
    public StageRoll? StageRoll { get; set; }

    public required ScoreFormat ScoreFormat { get; set; }

    /// <summary>
    /// How many Teams a Division normally holds, for a Divisional League of
    /// the Team-based Types (ADR-0031): the Start screen proposes Divisions of
    /// this size, and the Manager may make any a Team bigger or smaller. Null
    /// for Knockout, which has no Divisions, and Foursomes, whose Divisions are
    /// always 4 Players (#14).
    /// </summary>
    public int? DivisionSize { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<LeagueParticipant> Participants { get; set; } = [];

    public List<LeagueJoinRequest> JoinRequests { get; set; } = [];

    public List<Team> Teams { get; set; } = [];

    /// <summary>Its Stages, Stage 1 first. Starting the League opens Stage 1 (#7); none before that.</summary>
    public List<Stage> Stages { get; set; } = [];

    /// <summary>
    /// The one Type×Structure combination that is invalid: Foursomes is a
    /// Division-of-4 partner rotation, meaningless in a Bracket.
    /// </summary>
    public static bool IsValidCombination(LeagueType type, LeagueStructure structure) =>
        !(type == LeagueType.Foursomes && structure == LeagueStructure.Knockout);
}
