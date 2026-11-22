namespace SocialTennis.Api.Domain;

/// <summary>What a Score said at one moment: the before and after of a <see cref="ScoreChange"/>.</summary>
/// <remarks>Properties rather than a constructor: EF Core can't bind a complex collection to a constructor parameter.</remarks>
public sealed record ScoreValue
{
    public DateOnly PlayedOn { get; init; }

    public List<ScoreSet> Sets { get; init; } = [];

    public int Winner { get; init; }

    public bool Retired { get; init; }

    public static ScoreValue Of(Score score) =>
        new() { PlayedOn = score.PlayedOn, Sets = [.. score.Sets], Winner = score.Winner, Retired = score.Retired };
}
