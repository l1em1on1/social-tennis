namespace SocialTennis.Api.Domain;

/// <summary>
/// Two Sides playing a match (see CONTEXT.md, Game): here a Division's Game in
/// a League Stage, made when the Stage opens (#7). Its <see cref="Score"/> is
/// entered once it's played (#38); arranging a Date and a Court is #9.
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

    /// <summary>None until a Player who played it enters one (#38).</summary>
    public Score? Score { get; set; }
}
