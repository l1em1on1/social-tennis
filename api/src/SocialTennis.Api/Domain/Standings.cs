using System.Text.Json.Serialization;

namespace SocialTennis.Api.Domain;

/// <summary>
/// A Division's Standings (#10; see CONTEXT.md, Standings): its Teams ranked
/// by their Games' Scores, and who would move if the Stage closed now. Pure,
/// so they are unit-tested; computed whenever they're read, so they follow
/// every Score entered, corrected or overridden.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Order: matches won, or points in <see cref="StandingsMode.BonusPoints"/> (#214), then games
/// difference, then head-to-head: the Game between two tied Teams, or among three or more the wins in
/// the Games between them, again for any still tied. What's left tied is the Manager's to decide
/// (#10 part 2).</item>
/// <item>Points (#214): 1 for a match won, 1 for a match played, and 1 more for playing every one of
/// the Team's Games in the Division. A Game with no Score, or not played, gives neither Side a point,
/// and keeps both from the last one.</item>
/// <item>A Game with no Score counts for nothing either way (owner, 2026-11-22). A Retirement is a
/// played result: its winner is declared, and its games count as they stood.</item>
/// <item>A Set's games count as recorded, 6–5 on a tiebreak included. A Match Tiebreak counts as one
/// game, to its winner.</item>
/// <item>At close each Division's top Team moves up one and its bottom Team down one; the top of
/// the ladder and its bottom stay put (ADR-0031).</item>
/// </list>
/// </remarks>
public static class Standings
{
    /// <summary>
    /// The games each Side won in a Score, for games difference: a Match
    /// Tiebreak is one game to its winner, and an unfinished one counts nothing.
    /// </summary>
    public static (int First, int Second) GamesOf(ScoreFormat format, IReadOnlyList<ScoreSet> sets)
    {
        var (first, second) = (0, 0);
        for (var index = 0; index < sets.Count; index++)
        {
            var set = sets[index];
            if (!format.IsMatchTiebreak(index))
            {
                first += set.First;
                second += set.Second;
            }
            else if (Math.Max(set.First, set.Second) >= ScoreRules.MatchTiebreakPoints && Math.Abs(set.First - set.Second) >= 2)
            {
                first += set.First > set.Second ? 1 : 0;
                second += set.Second > set.First ? 1 : 0;
            }
        }

        return (first, second);
    }

    /// <summary>A Score as Standings count it: its winning Side, and the games each Side won.</summary>
    public static StandingsResult ResultOf(ScoreFormat format, int winner, IReadOnlyList<ScoreSet> sets)
    {
        var (first, second) = GamesOf(format, sets);
        return new StandingsResult(winner, first, second);
    }

    /// <summary>
    /// Ranks a Division's <paramref name="teams"/>, given in ladder order, by its
    /// <paramref name="games"/>, every one of the Stage's, scored or not. Teams
    /// still tied share a rank, in ladder order.
    /// </summary>
    public static IReadOnlyList<Standing> Rank(
        IReadOnlyList<Guid> teams, IReadOnlyList<StandingsGame> games, StandingsMode mode = StandingsMode.Standard)
    {
        var rows = teams.ToDictionary(team => team, team => new Tally());
        foreach (var game in games)
        {
            rows[game.FirstTeamId].Games++;
            rows[game.SecondTeamId].Games++;
            if (game.Result is not { } result)
            {
                continue;
            }

            var (winner, loser) = result.Winner == 1 ? (game.FirstTeamId, game.SecondTeamId) : (game.SecondTeamId, game.FirstTeamId);
            rows[winner].Won++;
            rows[loser].Lost++;
            rows[game.FirstTeamId].GamesFor += result.FirstGames;
            rows[game.FirstTeamId].GamesAgainst += result.SecondGames;
            rows[game.SecondTeamId].GamesFor += result.SecondGames;
            rows[game.SecondTeamId].GamesAgainst += result.FirstGames;
        }

        var ladderOrder = teams.Select((team, index) => (team, index)).ToDictionary(pair => pair.team, pair => pair.index);
        int? PointsOf(Guid team) => mode == StandingsMode.BonusPoints ? rows[team].Points : null;
        var groups = teams
            .GroupBy(team => (First: PointsOf(team) ?? rows[team].Won, Difference: rows[team].GamesFor - rows[team].GamesAgainst))
            .OrderByDescending(group => group.Key.First)
            .ThenByDescending(group => group.Key.Difference)
            .SelectMany(group => HeadToHead([.. group], games))
            .ToList();

        var standings = new List<Standing>();
        foreach (var group in groups)
        {
            var rank = standings.Count + 1;
            standings.AddRange(group
                .OrderBy(team => ladderOrder[team])
                .Select(team => new Standing(
                    team, rank, rows[team].Played, rows[team].Won, rows[team].Lost, rows[team].GamesFor, rows[team].GamesAgainst, group.Count > 1, PointsOf(team))));
        }

        return standings;
    }

    /// <summary>
    /// Where a Team would go if the Stage closed now (ADR-0031): up from first
    /// place, down from last, unless it's the top or bottom Division. None when
    /// that place is tied, for the Manager to decide. Before a Game in the
    /// Division has a Score, nobody's place means anything yet, so none.
    /// </summary>
    public static StandingMove? MoveOf(Standing standing, IReadOnlyList<Standing> division, int number, int divisions)
    {
        if (division.All(row => row.Played == 0))
        {
            return null;
        }

        // A tie for first or last decides who moves; a tie spanning both does both.
        var movesUp = standing.Rank == 1 && number > 1;
        var movesDown = standing.Rank == division.Max(row => row.Rank) && number < divisions;
        if (!movesUp && !movesDown)
        {
            return StandingMove.Stays;
        }

        return standing.Tied ? null : movesUp ? StandingMove.Up : StandingMove.Down;
    }

    /// <summary>
    /// The ties a Manager must order before the Stage closes (#10): Teams
    /// sharing a place that moves, first in any Division but the top or last
    /// in any but the bottom. A Division where nothing was played is one such
    /// tie of all its Teams, ordered like any other (#10, part 2).
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<Guid>> OpenTies(IReadOnlyList<Standing> division, int number, int divisions)
    {
        var last = division.Max(row => row.Rank);
        return
        [
            .. division
                .Where(row => row.Tied && ((row.Rank == 1 && number > 1) || (row.Rank == last && number < divisions)))
                .GroupBy(row => row.Rank)
                .Select(group => (IReadOnlyList<Guid>)[.. group.Select(row => row.TeamId)]),
        ];
    }

    /// <summary>Splits a group tied on wins and games difference by the Games among them, as far as they go.</summary>
    private static IEnumerable<List<Guid>> HeadToHead(List<Guid> group, IReadOnlyList<StandingsGame> games)
    {
        if (group.Count == 1)
        {
            yield return group;
            yield break;
        }

        var wins = group.ToDictionary(team => team, _ => 0);
        foreach (var game in games)
        {
            if (game.Result is { } result && wins.ContainsKey(game.FirstTeamId) && wins.ContainsKey(game.SecondTeamId))
            {
                wins[result.Winner == 1 ? game.FirstTeamId : game.SecondTeamId]++;
            }
        }

        var split = group.GroupBy(team => wins[team]).OrderByDescending(byWins => byWins.Key).Select(byWins => byWins.ToList()).ToList();
        if (split.Count == 1)
        {
            // Nothing between them decides it: the Manager does.
            yield return group;
            yield break;
        }

        foreach (var part in split)
        {
            foreach (var resolved in HeadToHead(part, games))
            {
                yield return resolved;
            }
        }
    }

    private sealed class Tally
    {
        /// <summary>Its Games in the Division this Stage, scored or not.</summary>
        public int Games { get; set; }

        public int Won { get; set; }

        public int Lost { get; set; }

        public int Played => Won + Lost;

        /// <summary>A point a win, a point a match played, and one more for playing them all (#214).</summary>
        public int Points => Won + Played + (Games > 0 && Played == Games ? 1 : 0);

        public int GamesFor { get; set; }

        public int GamesAgainst { get; set; }
    }
}

/// <summary>A Division's Game, for its Standings: none if it has no Score.</summary>
public sealed record StandingsGame(Guid FirstTeamId, Guid SecondTeamId, StandingsResult? Result);

/// <summary>A Game's result, for Standings: the winning Side, 1 or 2, and the games each Side won (<see cref="Standings.GamesOf"/>).</summary>
public sealed record StandingsResult(int Winner, int FirstGames, int SecondGames);

/// <summary>A Team's line in its Division's Standings.</summary>
/// <param name="Rank">1 for first; Teams still tied share one.</param>
/// <param name="Tied">Shares its rank with another Team: the tie-breaks don't separate them.</param>
/// <param name="Points">Its points in <see cref="StandingsMode.BonusPoints"/> (#214); null in Standard.</param>
public sealed record Standing(Guid TeamId, int Rank, int Played, int Won, int Lost, int GamesFor, int GamesAgainst, bool Tied, int? Points = null);

/// <summary>Where a Team goes at close (ADR-0031).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StandingMove>))]
public enum StandingMove
{
    Up = 1,
    Down = 2,
    Stays = 3,
}
