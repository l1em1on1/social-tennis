namespace SocialTennis.Api.Domain;

/// <summary>
/// One of a Game's two Sides (see CONTEXT.md, Side): a Team in the Team-based
/// Types. Side 1 is the Competitor higher on the ladder. Foursomes (#14), whose
/// Sides are a pair for one Game, will record Players instead.
/// </summary>
public class GameSide
{
    public Guid GameId { get; set; }

    public Game Game { get; set; } = null!;

    /// <summary>1 or 2.</summary>
    public int Side { get; set; }

    /// <summary>With <see cref="GameId"/> and <see cref="TeamId"/>, keeps the Game and the Team in one League.</summary>
    public Guid LeagueId { get; set; }

    public Guid TeamId { get; set; }

    public Team Team { get; set; } = null!;
}
