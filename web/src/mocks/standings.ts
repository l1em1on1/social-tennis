import type { components } from "@/lib/api/schema";
import { isMatchTiebreak, MATCH_TIEBREAK_POINTS, type ScoreFormat, type ScoreSet } from "@/lib/score";

type Standing = components["schemas"]["LadderStanding"];

/**
 * The mock's copy of the API's Standings (#10, Domain/Standings.cs): the same
 * order and tie-breaks, so a mocked run ranks as the real one does. The web
 * app itself only shows what the API sends.
 */
interface Game {
  first: string;
  second: string;
  result: { winner: 1 | 2; firstGames: number; secondGames: number } | null;
}

/** As Standings.GamesOf: a Match Tiebreak is one game to its winner; an unfinished one counts nothing. */
export function gamesOf(format: ScoreFormat, sets: ScoreSet[]): [number, number] {
  let [first, second] = [0, 0];
  sets.forEach((set, index) => {
    if (!isMatchTiebreak(format, index)) {
      first += set.first;
      second += set.second;
    } else if (Math.max(set.first, set.second) >= MATCH_TIEBREAK_POINTS && Math.abs(set.first - set.second) >= 2) {
      first += set.first > set.second ? 1 : 0;
      second += set.second > set.first ? 1 : 0;
    }
  });
  return [first, second];
}

function headToHead(group: string[], games: Game[]): string[][] {
  if (group.length === 1) return [group];
  const wins = new Map(group.map((team) => [team, 0]));
  for (const game of games) {
    if (game.result && wins.has(game.first) && wins.has(game.second)) {
      const winner = game.result.winner === 1 ? game.first : game.second;
      wins.set(winner, wins.get(winner)! + 1);
    }
  }
  const counts = [...new Set(group.map((team) => wins.get(team)!))].sort((a, b) => b - a);
  if (counts.length === 1) return [group];
  return counts.flatMap((count) => headToHead(group.filter((team) => wins.get(team) === count), games));
}

/** As Standings.Rank and MoveOf: `teams` in ladder order, for Division `number` of `divisions`. */
export function standingsOf(teams: string[], games: Game[], number: number, divisions: number): Standing[] {
  const rows = new Map(teams.map((team) => [team, { won: 0, lost: 0, gamesFor: 0, gamesAgainst: 0 }]));
  for (const { first, second, result } of games) {
    if (!result) continue;
    const [winner, loser] = result.winner === 1 ? [first, second] : [second, first];
    rows.get(winner)!.won++;
    rows.get(loser)!.lost++;
    rows.get(first)!.gamesFor += result.firstGames;
    rows.get(first)!.gamesAgainst += result.secondGames;
    rows.get(second)!.gamesFor += result.secondGames;
    rows.get(second)!.gamesAgainst += result.firstGames;
  }
  const keyOf = (team: string) => {
    const row = rows.get(team)!;
    return [row.won, row.gamesFor - row.gamesAgainst] as const;
  };
  const keys = [...new Set(teams.map((team) => keyOf(team).join(",")))]
    .map((key) => key.split(",").map(Number))
    .sort((a, b) => b[0] - a[0] || b[1] - a[1]);
  const groups = keys.flatMap(([won, difference]) =>
    headToHead(teams.filter((team) => keyOf(team)[0] === won && keyOf(team)[1] === difference), games),
  );

  const standings: Omit<Standing, "move">[] = [];
  for (const group of groups) {
    const rank = standings.length + 1;
    for (const team of [...group].sort((a, b) => teams.indexOf(a) - teams.indexOf(b))) {
      const row = rows.get(team)!;
      standings.push({ teamId: team, rank, played: row.won + row.lost, ...row, tied: group.length > 1 });
    }
  }
  const results = standings.some((standing) => Number(standing.played) > 0);
  const last = Math.max(...standings.map((standing) => Number(standing.rank)));
  return standings.map((standing) => {
    const up = standing.rank === 1 && number > 1;
    const down = standing.rank === last && number < divisions;
    const move = !results ? null : !up && !down ? "Stays" : standing.tied ? null : up ? "Up" : "Down";
    return { ...standing, move };
  });
}
