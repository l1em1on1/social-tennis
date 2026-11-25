import type { components } from "@/lib/api/schema";
import { cx } from "@/lib/cx";

type SetScore = components["schemas"]["SetScore"];

/** One Side's cell in a Set: its games, a Set lost on a tiebreak with its points, a Match Tiebreak in brackets. */
function cell(set: SetScore, side: 0 | 1): string {
  const games = Number(side === 0 ? set.first : set.second);
  if (set.matchTiebreak) {
    return `[${games}]`;
  }
  const mine = side === 0 ? set.firstTiebreak : set.secondTiebreak;
  const theirs = side === 0 ? set.secondTiebreak : set.firstTiebreak;
  return mine !== null && mine !== undefined && theirs !== null && theirs !== undefined && Number(mine) < Number(theirs)
    ? `${games}(${Number(mine)})`
    : `${games}`;
}

/**
 * A Game's Score set by set (#38, wireframe P3): a row per Side, the winner's
 * in bold with a tick and named in words below, so colour is never the only
 * signal.
 */
export function ScoreTable({ sides, sets, winner }: { sides: [string, string]; sets: SetScore[]; winner: number }) {
  return (
    <table className="w-full border-collapse text-left">
      <caption className="sr-only">Score, set by set</caption>
      <thead className="sr-only">
        <tr>
          <th scope="col">Side</th>
          {sets.map((set, index) => (
            <th key={index} scope="col">
              {set.matchTiebreak ? "Match tiebreak" : `Set ${index + 1}`}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {sides.map((name, side) => {
          const won = winner === side + 1;
          return (
            <tr key={side} className={cx("border-b border-border last:border-0", won ? "font-bold" : "text-fg-muted")}>
              <th scope="row" className="py-2 pr-3 font-[weight:inherit] wrap-anywhere">
                {name} {won && <span aria-label="won">✓</span>}
              </th>
              {sets.map((set, index) => (
                <td key={index} className="w-14 py-2 text-center font-display text-3xl leading-none font-black tabular-nums">
                  {cell(set, side as 0 | 1)}
                </td>
              ))}
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
