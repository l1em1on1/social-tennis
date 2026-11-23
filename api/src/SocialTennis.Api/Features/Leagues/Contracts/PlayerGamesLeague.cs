namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A League the Player is in, with its current Stage and their Games in it (#42).</summary>
/// <param name="Stage">The current Stage: its Games are due by the end of its EndDate (ADR-0016).</param>
/// <param name="Division">The Player's Division this Stage; none when they aren't on the ladder.</param>
/// <param name="HeldUp">Past its deadline and still open (#10): it waits on the Manager.</param>
/// <param name="UnscoredGames">Games in the whole League this Stage with no Score that aren't marked not played.</param>
/// <param name="OpenTies">Ties in the League that decide who moves, for the Manager to order at close.</param>
/// <param name="Games">The Player's own Games this Stage, in ladder order.</param>
public record PlayerGamesLeague(
    Guid LeagueId,
    string LeagueName,
    string ClubName,
    LeagueStageSummary Stage,
    int? Division,
    bool HeldUp,
    int UnscoredGames,
    int OpenTies,
    IReadOnlyList<PlayerGame> Games);
