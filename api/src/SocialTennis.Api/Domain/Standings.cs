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
    /// <remarks>
    /// <paramref name="up"/> and <paramref name="down"/> are the Players' marks
    /// on a Foursomes tie (#14 part 3, <see cref="TieMark"/>), already checked
    /// to be for the tie as it stands: the one marked moves, the others stay.
    /// </remarks>
    public static StandingMove? MoveOf(
        Standing standing, IReadOnlyList<Standing> division, int number, int divisions, Guid? up = null, Guid? down = null)
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

        if (!standing.Tied)
        {
            return movesUp ? StandingMove.Up : StandingMove.Down;
        }

        // A tie: each place it decides moves only the one the Players marked for it.
        if (movesUp && up == standing.TeamId)
        {
            return StandingMove.Up;
        }

        if (movesDown && down == standing.TeamId)
        {
            return StandingMove.Down;
        }

        return (!movesUp || up is not null) && (!movesDown || down is not null) ? StandingMove.Stays : null;
    }

    /// <summary>
    /// The ties a Manager must order before the Stage closes (#10): Teams
    /// sharing a place that moves, first in any Division but the top or last
    /// in any but the bottom. A Division where nothing was played is one such
    /// tie of all its Teams, ordered like any other (#10, part 2).
    /// </summary>
    /// <remarks>A tie the Players marked for every place it decides (<see cref="MoveOf"/>) no longer waits.</remarks>
    public static IReadOnlyList<IReadOnlyList<Guid>> OpenTies(
        IReadOnlyList<Standing> division, int number, int divisions, Guid? up = null, Guid? down = null)
    {
        var last = division.Max(row => row.Rank);
        return
        [
            .. division
                .Where(row => row.Tied && ((row.Rank == 1 && number > 1) || (row.Rank == last && number < divisions)))
                .GroupBy(row => row.Rank)
                .Where(group => !((group.Key != 1 || number == 1 || up is not null) && (group.Key != last || number == divisions || down is not null)))
                .Select(group => (IReadOnlyList<Guid>)[.. group.Select(row => row.TeamId)]),
        ];
    }

    /// <summary>
    /// A Division's Standings with the Players' marks (#14 part 3) applied:
    /// the one marked to go up first in its tie, the one to go down last.
    /// Ranks stay shared, so the Manager can still order the tie at the close.
    /// </summary>
    public static IReadOnlyList<Standing> OrderByMarks(IReadOnlyList<Standing> division, Guid? up, Guid? down)
    {
        int Order(Standing row) => row.TeamId == up ? 0 : row.TeamId == down ? 2 : 1;
        return [.. division.Select((row, index) => (row, index)).OrderBy(pair => pair.row.Rank).ThenBy(pair => Order(pair.row)).ThenBy(pair => pair.index).Select(pair => pair.row)];
    }

    /// <summary>
    /// The Teams level at a place that moves in a Division (#10): first, which
    /// decides who goes up in any Division but the top, or last, who goes down
    /// in any but the bottom. None if one Team holds it alone.
    /// </summary>
    public static IReadOnlyList<Guid>? TiedAt(IReadOnlyList<Standing> division, TieMarkPlace place, int number, int divisions)
    {
        if (place == TieMarkPlace.Up ? number == 1 : number == divisions)
        {
            return null;
        }

        var rank = place == TieMarkPlace.Up ? 1 : division.Max(row => row.Rank);
        var tied = division.Where(row => row.Rank == rank).Select(row => row.TeamId).ToList();
        return tied.Count > 1 ? tied : null;
    }

    /// <summary>
    /// Ranks a Foursomes Division's Players (#14, ADR-0036), given in ladder
    /// order, by the games they banked: the games their Side won in each of
    /// their Games, a Match Tiebreak as one. Played, won and lost count the
    /// Games their Side played, won and lost. Nothing else separates Players
    /// level on games: they settle it themselves (owner, 2026-11-26), so they
    /// share a rank, and where it decides a move it is an open tie. A Game not
    /// played leaves the Division's moves to the Manager (owner, 2026-11-26):
    /// every Player shares first.
    /// </summary>
    public static IReadOnlyList<Standing> RankFoursomes(IReadOnlyList<Guid> players, IReadOnlyList<PairsGame> games)
    {
        var rows = players.ToDictionary(player => player, _ => new Tally());
        foreach (var game in games)
        {
            if (game.Result is not { } result)
            {
                continue;
            }

            foreach (var (side, pair) in new[] { (1, game.First), (2, game.Second) })
            {
                var (banked, conceded) = side == 1 ? (result.FirstGames, result.SecondGames) : (result.SecondGames, result.FirstGames);
                foreach (var player in pair)
                {
                    rows[player].GamesFor += banked;
                    rows[player].GamesAgainst += conceded;
                    if (result.Winner == side)
                    {
                        rows[player].Won++;
                    }
                    else
                    {
                        rows[player].Lost++;
                    }
                }
            }
        }

        var managerDecides = games.Any(game => game.NotPlayed);
        var ladderOrder = players.Select((player, index) => (player, index)).ToDictionary(pair => pair.player, pair => pair.index);
        var groups = players
            .GroupBy(player => managerDecides ? 0 : rows[player].GamesFor)
            .OrderByDescending(group => group.Key)
            .Select(group => group.ToList())
            .ToList();
        var standings = new List<Standing>();
        foreach (var group in groups)
        {
            var rank = standings.Count + 1;
            standings.AddRange(group
                // A Division the Manager orders still lists the most games first; Players level on games, in ladder order.
                .OrderByDescending(player => rows[player].GamesFor)
                .ThenBy(player => ladderOrder[player])
                .Select(player => new Standing(
                    player, rank, rows[player].Played, rows[player].Won, rows[player].Lost, rows[player].GamesFor, rows[player].GamesAgainst, group.Count > 1)));
        }

        return standings;
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

/// <summary>
/// A Foursomes Game, for its Division's Standings (#14): the two Players on
/// each Side, its result if it has a Score, and whether the Manager marked it
/// not played.
/// </summary>
public sealed record PairsGame(IReadOnlyList<Guid> First, IReadOnlyList<Guid> Second, StandingsResult? Result, bool NotPlayed);

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
