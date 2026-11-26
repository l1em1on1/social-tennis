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
        TennisDbContext db, Guid stageId, ScoreFormat format, StandingsMode? mode, CancellationToken cancellationToken)
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
                First = g.Sides.Where(s => s.Side == 1).Select(s => s.TeamId).Single(),
                Second = g.Sides.Where(s => s.Side == 2).Select(s => s.TeamId).Single(),
                Score = g.Score == null ? null : new { g.Score.Winner, g.Score.Retired, g.Score.Sets },
                NotPlayed = g.VoidedAt != null,
            })
            .ToListAsync(cancellationToken);

        // Each Division's Games in ladder order: by the higher Team's place, then the lower's.
        var positionOf = places.ToDictionary(p => p.Competitor.TeamId, p => p.Competitor.Position);
        var count = places.Select(p => p.Division).Distinct().Count();
        var ranked = places
            .GroupBy(p => p.Division)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var inDivision = games.Where(g => g.Division == group.Key).OrderBy(g => positionOf[g.First]).ThenBy(g => positionOf[g.Second]).ToList();
                var standings = Standings.Rank(
                    [.. group.Select(p => p.Competitor.TeamId)],
                    [
                        .. inDivision.Select(g => new StandingsGame(
                            g.First,
                            g.Second,
                            g.Score is { } score ? Standings.ResultOf(format, score.Winner, score.Sets) : null)),
                    ],
                    mode ?? StandingsMode.Standard);
                var division = new LadderDivision(
                    group.Key,
                    [.. group.Select(p => p.Competitor)],
                    [
                        .. inDivision.Select(g => new LadderGame(
                            g.Id,
                            g.First,
                            g.Second,
                            g.Score is { } score
                                ? new LadderGameScore(score.Winner, score.Retired, SetScore.Of(format, score.Sets))
                                : null,
                            g.NotPlayed)),
                    ],
                    [
                        .. standings.Select(s => new LadderStanding(
                            s.TeamId, s.Rank, s.Played, s.Won, s.Lost, s.GamesFor, s.GamesAgainst, s.Tied,
                            Standings.MoveOf(s, standings, group.Key, count),
                            s.Points)),
                    ]);
                return (Division: division, Ties: Standings.OpenTies(standings, group.Key, count).Select(tie => new LadderTie(group.Key, tie)));
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
    /// pair in a Division a Game, the higher place as Side 1 (ADR-0031). For
    /// Stage 1 at the start (#7), and each Stage after a close (#10).
    /// </summary>
    public static void Place(Stage stage, Guid leagueId, IReadOnlyList<IReadOnlyList<Guid>> divisions)
    {
        var position = 0;
        for (var division = 1; division <= divisions.Count; division++)
        {
            var members = divisions[division - 1];
            foreach (var teamId in members)
            {
                stage.Places.Add(new LadderPlace { LeagueId = leagueId, Position = ++position, Division = division, TeamId = teamId });
            }

            foreach (var (first, second) in Ladder.RoundRobin(members.Count))
            {
                var game = new Game { Id = Guid.NewGuid(), LeagueId = leagueId, Division = division };
                game.Sides.Add(new GameSide { LeagueId = leagueId, Side = 1, TeamId = members[first] });
                game.Sides.Add(new GameSide { LeagueId = leagueId, Side = 2, TeamId = members[second] });
                stage.Games.Add(game);
            }
        }
    }
}
