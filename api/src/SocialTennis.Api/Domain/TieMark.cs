using System.Text.Json.Serialization;

namespace SocialTennis.Api.Domain;

/// <summary>
/// The Players' answer to a Foursomes tie that decides a move (#14 part 3,
/// ADR-0036; owner, 2026-11-26): who goes up, or who goes down, of the
/// Players level on games there, settled however they like (a coin toss).
/// It holds only while the same Players are level at that place
/// (<see cref="Group"/>): a Score that changes the tie leaves it aside, and
/// the ladder asks again. The last answer stands, and the Manager can still
/// order the tie at the close.
/// </summary>
public class TieMark
{
    public Guid StageId { get; set; }

    public Stage Stage { get; set; } = null!;

    public Guid LeagueId { get; set; }

    public int Division { get; set; }

    public TieMarkPlace Place { get; set; }

    /// <summary>The Players' Teams of one level at that place when it was marked, in id order.</summary>
    public Guid[] Group { get; set; } = [];

    /// <summary>The one who goes up, or down.</summary>
    public Guid TeamId { get; set; }

    /// <summary>The Player who said so; null once that Player is deleted (#116).</summary>
    public Guid? MarkedById { get; set; }

    public Player? MarkedBy { get; set; }

    public DateTimeOffset MarkedAt { get; set; }
}

/// <summary>The place a tie decides: who goes up from first, or down from last.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TieMarkPlace>))]
public enum TieMarkPlace
{
    Up = 1,
    Down = 2,
}
