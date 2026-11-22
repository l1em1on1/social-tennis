import type { components } from "@/lib/api/schema";

type Schemas = components["schemas"];

/**
 * A Score's rules (#38), mirrored from the API's `ScoreRules` message for
 * message: the entry screen's hints and the MSW mock use them, and the API
 * stays the authority. Change both together (ScoreRulesTests pins the API's).
 */

/** A League's Score format, with the API's `number | string` integers made numbers. */
export interface ScoreFormat {
  gamesPerSet: number;
  setsPerGame: number;
  matchTiebreakDecider: boolean;
}

export const scoreFormat = (format: Schemas["ScoreFormat"]): ScoreFormat => ({
  gamesPerSet: Number(format.gamesPerSet),
  setsPerGame: Number(format.setsPerGame),
  matchTiebreakDecider: format.matchTiebreakDecider,
});

/**
 * One Set as recorded: Side 1's games first. A Set won on a tiebreak is 6–5
 * (or 9–8) with both Sides' points; a Match Tiebreak's games are its points.
 */
export interface ScoreSet {
  first: number;
  second: number;
  firstTiebreak: number | null;
  secondTiebreak: number | null;
}

export type Side = 1 | 2;

export type ScoreCheck = { ok: true; winner: Side } | { ok: false; field: string; message: string };

export type SetResult =
  | { state: "won"; winner: Side }
  | { state: "unfinished"; message: string }
  | { state: "impossible"; message: string };

export const TIEBREAK_POINTS = 7;
export const MATCH_TIEBREAK_POINTS = 10;

/** Whether the Set at `index` (0 for the first) is played as a Match Tiebreak. */
export const isMatchTiebreak = (format: ScoreFormat, index: number) =>
  format.matchTiebreakDecider && format.setsPerGame === 3 && index === 2;

type NotWon = Exclude<SetResult, { state: "won" }>;

/** A first-to-`target`, 2-clear race: null once won, or why it isn't. */
function race(high: number, low: number, target: number, name: string): NotWon | null {
  if (high < target) {
    return { state: "unfinished", message: `A ${name} is first to ${target} points.` };
  }
  if (high > target && high - low > 2) {
    return { state: "impossible", message: `Past ${target}, a ${name} ends as soon as someone is 2 points clear.` };
  }
  return high - low < 2
    ? { state: "unfinished", message: `A ${name} needs a 2-point lead, like ${target}–${target - 2} or ${target + 1}–${target - 1}.` }
    : null;
}

export function checkSet(format: ScoreFormat, set: ScoreSet, matchTiebreak: boolean): SetResult {
  const high = Math.max(set.first, set.second);
  const low = Math.min(set.first, set.second);
  const leader: Side = set.first > set.second ? 1 : 2;
  const hasPoints = set.firstTiebreak !== null || set.secondTiebreak !== null;
  if (matchTiebreak) {
    if (hasPoints) {
      return { state: "impossible", message: "A match tiebreak's points are its score: it has no tiebreak of its own." };
    }
    return race(high, low, MATCH_TIEBREAK_POINTS, "match tiebreak") ?? { state: "won", winner: leader };
  }

  const games = format.gamesPerSet;
  if (high > games) {
    return { state: "impossible", message: `A set ends at ${games} games.` };
  }

  if (high === games && low === games - 1) {
    if (set.firstTiebreak === null || set.secondTiebreak === null) {
      return {
        state: "impossible",
        message: `A ${games}–${games - 1} set was won on a tiebreak at ${games - 1}–${games - 1}: enter its points.`,
      };
    }
    const problem = race(
      Math.max(set.firstTiebreak, set.secondTiebreak),
      Math.min(set.firstTiebreak, set.secondTiebreak),
      TIEBREAK_POINTS,
      "tiebreak",
    );
    if (problem) {
      // Unfinished points can't have won the Set.
      return { state: "impossible", message: problem.message };
    }
    return (set.firstTiebreak > set.secondTiebreak ? 1 : 2) === leader
      ? { state: "won", winner: leader }
      : { state: "impossible", message: "The side that won the tiebreak wins the set." };
  }

  if (high === games - 1 && low === games - 1) {
    if (!hasPoints) {
      return { state: "unfinished", message: `At ${games - 1}–${games - 1} the set goes to a tiebreak: enter its points.` };
    }
    if (set.firstTiebreak === null || set.secondTiebreak === null) {
      return { state: "impossible", message: "Enter both sides' tiebreak points." };
    }
    // A tiebreak still being played, in a Retirement; a finished one wins the Set.
    return (
      race(Math.max(set.firstTiebreak, set.secondTiebreak), Math.min(set.firstTiebreak, set.secondTiebreak), TIEBREAK_POINTS, "tiebreak") ?? {
        state: "impossible",
        message: `A finished tiebreak wins the set ${games}–${games - 1}.`,
      }
    );
  }

  if (hasPoints) {
    return { state: "impossible", message: `Only a set won ${games}–${games - 1} has tiebreak points.` };
  }
  if (high === games) {
    return low <= games - 2
      ? { state: "won", winner: leader }
      : { state: "impossible", message: `${games}–${games} can't happen: at ${games - 1}–${games - 1} the set goes to a tiebreak.` };
  }
  return { state: "unfinished", message: `A set ends at ${games} games, or ${games}–${games - 1} on a tiebreak.` };
}

/**
 * Checks `sets`, in the order played, against `format`. `retirementWinner` is
 * the declared winning Side of a Retirement, or null for a Game played out.
 */
export function checkScore(format: ScoreFormat, sets: ScoreSet[], retirementWinner: Side | null): ScoreCheck {
  if (sets.length === 0) {
    return { ok: false, field: "sets", message: "Enter the score of each set." };
  }
  if (sets.length > format.setsPerGame) {
    return {
      ok: false,
      field: "sets",
      message: format.setsPerGame === 1 ? "This league plays one set." : "This league plays best of three sets.",
    };
  }

  const toWin = format.setsPerGame === 1 ? 1 : 2;
  const won = [0, 0];
  for (let index = 0; index < sets.length; index++) {
    if (Math.max(...won) === toWin) {
      return { ok: false, field: `sets[${index}]`, message: "The game was already won: there's no set after that." };
    }
    const set = checkSet(format, sets[index], isMatchTiebreak(format, index));
    const lastOfRetirement = retirementWinner !== null && index === sets.length - 1;
    if (set.state === "won") {
      won[set.winner - 1]++;
    } else if (!(set.state === "unfinished" && lastOfRetirement)) {
      return { ok: false, field: `sets[${index}]`, message: set.message };
    }
  }

  const finished = Math.max(...won) === toWin;
  if (retirementWinner !== null) {
    return finished
      ? { ok: false, field: "sets", message: "These sets finish the game, so it wasn't a retirement." }
      : { ok: true, winner: retirementWinner };
  }
  return finished
    ? { ok: true, winner: won[0] === toWin ? 1 : 2 }
    : { ok: false, field: "sets", message: "The game isn't finished: enter the rest of the sets, or record a retirement." };
}

/** A Set as the app shows it: 6–4, 6–5(4) with the loser's tiebreak points, [10–7] for a Match Tiebreak. */
export function formatSet(set: Pick<Schemas["SetScore"], "first" | "second" | "firstTiebreak" | "secondTiebreak" | "matchTiebreak">): string {
  const games = `${Number(set.first)}–${Number(set.second)}`;
  if (set.matchTiebreak) {
    return `[${games}]`;
  }
  if (set.firstTiebreak === null || set.secondTiebreak === null || set.firstTiebreak === undefined || set.secondTiebreak === undefined) {
    return games;
  }
  return `${games}(${Math.min(Number(set.firstTiebreak), Number(set.secondTiebreak))})`;
}

export const formatSets = (sets: Schemas["SetScore"][]) => sets.map(formatSet).join(" ");

/**
 * One Set on the entry screen, as it was played: at the tiebreak score the
 * games stay level (5–5) and the points are entered beside them.
 */
export interface SetEntry {
  first: number;
  second: number;
  firstPoints: number;
  secondPoints: number;
}

export const emptySet = (): SetEntry => ({ first: 0, second: 0, firstPoints: 0, secondPoints: 0 });

/**
 * Whether the entry is at the tiebreak score, so its points are asked for:
 * 5–5 as played, or 6–5 either way as it's recorded, which is how a Player
 * stepping up to the final score gets there.
 */
export function atTiebreak(format: ScoreFormat, entry: SetEntry, index: number): boolean {
  const games = format.gamesPerSet;
  const high = Math.max(entry.first, entry.second);
  const low = Math.min(entry.first, entry.second);
  return !isMatchTiebreak(format, index) && low === games - 1 && (high === games - 1 || high === games);
}

/**
 * The entry as recorded: a finished tiebreak at 5–5 makes the Set 6–5 to its
 * winner, with the points; an unfinished one keeps 5–5 and any points so far.
 * At 6–5 the points go with the games as they are, and the rules say whether
 * the tiebreak's winner won the Set.
 */
export function recordedSet(format: ScoreFormat, entry: SetEntry, index: number): ScoreSet {
  if (!atTiebreak(format, entry, index)) {
    return { first: entry.first, second: entry.second, firstTiebreak: null, secondTiebreak: null };
  }
  const { firstPoints, secondPoints } = entry;
  if (firstPoints === 0 && secondPoints === 0) {
    return { first: entry.first, second: entry.second, firstTiebreak: null, secondTiebreak: null };
  }
  if (entry.first !== entry.second) {
    return { first: entry.first, second: entry.second, firstTiebreak: firstPoints, secondTiebreak: secondPoints };
  }
  const finished = race(Math.max(firstPoints, secondPoints), Math.min(firstPoints, secondPoints), TIEBREAK_POINTS, "tiebreak") === null;
  const games = format.gamesPerSet;
  return finished
    ? {
        first: firstPoints > secondPoints ? games : games - 1,
        second: firstPoints > secondPoints ? games - 1 : games,
        firstTiebreak: firstPoints,
        secondTiebreak: secondPoints,
      }
    : { first: entry.first, second: entry.second, firstTiebreak: firstPoints, secondTiebreak: secondPoints };
}

const isEmpty = (set: ScoreSet) => set.first === 0 && set.second === 0 && set.firstTiebreak === null && set.secondTiebreak === null;

/**
 * How many Sets the entry screen shows: one, or in best of three two, and the
 * third only at one set all.
 */
export function shownSets(format: ScoreFormat, entries: SetEntry[]): number {
  if (format.setsPerGame === 1) {
    return 1;
  }
  const winners = [0, 1].map((index) => {
    const result = checkSet(format, recordedSet(format, entries[index], index), false);
    return result.state === "won" ? result.winner : null;
  });
  return winners[0] && winners[1] && winners[0] !== winners[1] ? 3 : 2;
}

/** The Sets to send: those shown, less any untouched ones after the first. */
export function setsToSend(format: ScoreFormat, entries: SetEntry[]): ScoreSet[] {
  const sets = entries.slice(0, shownSets(format, entries)).map((entry, index) => recordedSet(format, entry, index));
  while (sets.length > 1 && isEmpty(sets[sets.length - 1])) {
    sets.pop();
  }
  return sets;
}
