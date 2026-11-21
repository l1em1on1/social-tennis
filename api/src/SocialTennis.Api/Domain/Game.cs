namespace SocialTennis.Api.Domain;

/// <summary>
/// Two Sides playing a match (see CONTEXT.md, Game): here a Division's Game in
/// a League Stage, made when the Stage opens (#7). The Date it's played, a
/// Court and a Score come later (#9, #38).
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
}
