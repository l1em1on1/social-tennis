namespace SocialTennis.Api.Domain;

/// <summary>
/// A Team on one of a Game's two Sides (see CONTEXT.md, Side). In the
/// Team-based Types each Side is one Team, and Side 1 is the one higher on the
/// ladder. In Foursomes (#14, ADR-0036) each Side is two Players' Teams of one,
/// paired for this Game only, so a Side has two rows; Side 1 is the pair with
/// the Division's first place.
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
