using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Contracts;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Leagues.Contracts;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// A Stage's ladder as Players and Managers see it, and as its close counts
/// it (#7, #10): its Divisions from the top, each with its Teams, its Games
/// and their Scores, and its Standings; and what waits on the Manager. One
/// place, so the ladder and the close screen can't rank differently.
/// </summary>
public static class StageLadder
{
    public static async Task<(IReadOnlyList<LadderDivision> Divisions, LadderAwaiting Awaiting)> BuildAsync(
        TennisDbContext db, Guid stageId, ScoreFormat format, LeagueType type, StandingsMode? mode, CancellationToken cancellationToken)
    {
        var places = await db.LadderPlaces
            .Where(p => p.StageId == stageId)
            .OrderBy(p => p.Position)
            .Select(p => new
            {
                p.Division,
                Competitor = new LadderCompetitor(
                    p.TeamId,
                    p.Position,
                    p.Team.Members
                        .OrderBy(m => m.JoinedAt)
                        .ThenBy(m => m.PlayerId)
                        .Select(m => new LadderPlayer(m.PlayerId, m.Player.Name))
                        .ToList()),
            })
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        var games = await db.Games
            .Where(g => g.StageId == stageId)
            .Select(g => new
            {
                g.Id,
                g.Division,
                // A Side is one Team, or in Foursomes two Players' Teams of one (ADR-0036).
                Sides = g.Sides.Select(s => new { s.Side, s.TeamId }).ToList(),
                Score = g.Score == null ? null : new { g.Score.Winner, g.Score.Retired, g.Score.Sets },
                NotPlayed = g.VoidedAt != null,
            })
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        // Each Side's Teams top first; each Division's Games in ladder order: by Side 1's places, then Side 2's.
        var positionOf = places.ToDictionary(p => p.Competitor.TeamId, p => p.Competitor.Position);
        var sided = games
            .Select(g => new
            {
                g.Id,
                g.Division,
                First = (IReadOnlyList<Guid>)[.. g.Sides.Where(s => s.Side == 1).Select(s => s.TeamId).OrderBy(team => positionOf[team])],
                Second = (IReadOnlyList<Guid>)[.. g.Sides.Where(s => s.Side == 2).Select(s => s.TeamId).OrderBy(team => positionOf[team])],
                g.Score,
                g.NotPlayed,
            })
            .ToList();
        // The Players' answers to Foursomes ties (#14 part 3), each kept only while its tie stands as it was.
        var marks = type == LeagueType.Foursomes
            ? await db.TieMarks
                .Where(m => m.StageId == stageId)
                .Select(m => new { m.Division, m.Place, m.Group, m.TeamId, MarkedBy = m.MarkedBy == null ? null : m.MarkedBy.Name, m.MarkedAt })
                .ToListAsync(cancellationToken)
            : [];
        var count = places.Select(p => p.Division).Distinct().Count();
        var ranked = places
            .GroupBy(p => p.Division)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var inDivision = sided
                    .Where(g => g.Division == group.Key)
                    .OrderBy(g => positionOf[g.First[0]])
                    // In Foursomes Side 1 always has the first place: 1+2, then 1+3, then 1+4.
                    .ThenBy(g => g.First.Count > 1 ? positionOf[g.First[1]] : 0)
                    .ThenBy(g => positionOf[g.Second[0]])
                    .ToList();
                var teams = (IReadOnlyList<Guid>)[.. group.Select(p => p.Competitor.TeamId)];
                var standings = type == LeagueType.Foursomes
                    ? Standings.RankFoursomes(
                        teams,
                        [
                            .. inDivision.Select(g => new PairsGame(
                                g.First,
                                g.Second,
                                g.Score is { } score ? Standings.ResultOf(format, score.Winner, score.Sets) : null,
                                g.NotPlayed)),
                        ])
                    : Standings.Rank(
                        teams,
                        [
                            .. inDivision.Select(g => new StandingsGame(
                                g.First[0],
                                g.Second[0],
                                g.Score is { } score ? Standings.ResultOf(format, score.Winner, score.Sets) : null)),
                        ],
                        mode ?? StandingsMode.Standard);

                // A Foursomes tie at a place that moves is the Players' to settle once every Game is scored,
                // and none was not played (that leaves the Division to the Manager): owner, 2026-11-26.
                var settledByPlayers = type == LeagueType.Foursomes && inDivision.All(g => g.Score is not null);
                var playersTies = new List<LadderPlayersTie>();
                Guid? up = null, down = null;
                foreach (var place in settledByPlayers ? [TieMarkPlace.Up, TieMarkPlace.Down] : Array.Empty<TieMarkPlace>())
                {
                    if (Standings.TiedAt(standings, place, group.Key, count) is not { } tied)
                    {
                        continue;
                    }

                    var mark = marks.SingleOrDefault(m => m.Division == group.Key && m.Place == place && m.Group.ToHashSet().SetEquals(tied));
                    if (mark is not null && place == TieMarkPlace.Up)
                    {
                        up = mark.TeamId;
                    }
                    else if (mark is not null)
                    {
                        down = mark.TeamId;
                    }

                    playersTies.Add(new LadderPlayersTie(place, tied, mark is null ? null : new LadderTieMark(mark.TeamId, mark.MarkedBy, mark.MarkedAt)));
                }

                standings = Standings.OrderByMarks(standings, up, down);
                var division = new LadderDivision(
                    group.Key,
                    [.. group.Select(p => p.Competitor)],
                    [
                        .. inDivision.Select(g => new LadderGame(
                            g.Id,
                            g.First[0],
                            g.Second[0],
                            g.Score is { } score
                                ? new LadderGameScore(score.Winner, score.Retired, SetScore.Of(format, score.Sets))
                                : null,
                            g.NotPlayed,
                            g.First.Count > 1 ? g.First[1] : null,
                            g.Second.Count > 1 ? g.Second[1] : null)),
                    ],
                    [
                        .. standings.Select(s => new LadderStanding(
                            s.TeamId, s.Rank, s.Played, s.Won, s.Lost, s.GamesFor, s.GamesAgainst, s.Tied,
                            Standings.MoveOf(s, standings, group.Key, count, up, down),
                            s.Points)),
                    ],
                    playersTies);
                return (Division: division, Ties: Standings.OpenTies(standings, group.Key, count, up, down).Select(tie => new LadderTie(group.Key, tie)));
            })
            .ToList();
        var divisions = ranked.Select(r => r.Division).ToList();

        // What waits on the Manager (#10): Games nobody scored or voided, and ties that decide a move.
        var awaiting = new LadderAwaiting(
            [.. divisions.SelectMany(d => d.Games).Where(g => g.Score is null && !g.NotPlayed).Select(g => g.Id)],
            [.. ranked.SelectMany(r => r.Ties)]);
        return (divisions, awaiting);
    }

    /// <summary>
    /// Places <paramref name="divisions"/> (Team ids, top first, each in
    /// ladder order) on <paramref name="stage"/>'s ladder, and makes every
    /// pair in a Division a Game, the higher place as Side 1 (ADR-0031). A
    /// Foursomes Division of 4 Players gets its 3 Games of rotating pairs
    /// instead (<see cref="Ladder.FoursomesRotation"/>, ADR-0036). For Stage 1
    /// at the start (#7), and each Stage after a close (#10).
    /// </summary>
    public static void Place(Stage stage, Guid leagueId, LeagueType type, IReadOnlyList<IReadOnlyList<Guid>> divisions)
    {
        var position = 0;
        for (var division = 1; division <= divisions.Count; division++)
        {
            var members = divisions[division - 1];
            foreach (var teamId in members)
            {
                stage.Places.Add(new LadderPlace { LeagueId = leagueId, Position = ++position, Division = division, TeamId = teamId });
            }

            IEnumerable<(int[] First, int[] Second)> pairings = type == LeagueType.Foursomes
                ? Ladder.FoursomesRotation
                : Ladder.RoundRobin(members.Count).Select(pair => (new[] { pair.First }, new[] { pair.Second }));
            foreach (var (first, second) in pairings)
            {
                var game = new Game { Id = Guid.NewGuid(), LeagueId = leagueId, Division = division };
                foreach (var (side, places) in new[] { (1, first), (2, second) })
                {
                    foreach (var place in places)
                    {
                        game.Sides.Add(new GameSide { LeagueId = leagueId, Side = side, TeamId = members[place] });
                    }
                }

                stage.Games.Add(game);
            }
        }
    }
}
