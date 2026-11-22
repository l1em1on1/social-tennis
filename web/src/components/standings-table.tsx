import type { components } from "@/lib/api/schema";
import { cx } from "@/lib/cx";

type Standing = components["schemas"]["LadderStanding"];

/** Where a Team would go if the Stage closed now, in words beside the arrow: colour is never the only signal. */
function Move({ standing, results }: { standing: Standing; results: boolean }) {
  if (!results) return null;
  switch (standing.move) {
    case "Up":
      return <span className="font-semibold whitespace-nowrap text-success">▲ up</span>;
    case "Down":
      return <span className="font-semibold whitespace-nowrap text-danger">▼ down</span>;
    case "Stays":
      return <span className="whitespace-nowrap text-fg-muted">stays</span>;
    default:
      return <span className="font-semibold whitespace-nowrap text-warning">tied</span>;
  }
}

const difference = (standing: Standing) => {
  const value = Number(standing.gamesFor) - Number(standing.gamesAgainst);
  return value > 0 ? `+${value}` : `${value}`;
};

/**
 * A Division's Standings (#10, wireframe P5): every Team from first, with
 * its Games played, won and lost and its games difference, and where it
 * would go if the Stage closed now. Teams the tie-breaks can't separate
 * share a rank (=2); where that decides who moves, it says "tied", for the
 * Manager to settle at close. Before any Score, it says so instead.
 */
export function StandingsTable({
  number,
  standings,
  names,
  mine = [],
}: {
  number: number;
  standings: Standing[];
  /** Each Team's name, by Team id. */
  names: Record<string, string>;
  /** The Acting Player's Team, shown in bold. */
  mine?: string[];
}) {
  const results = standings.some((standing) => Number(standing.played) > 0);
  return (
    <div className="flex flex-col gap-2">
      <table className="w-full border-collapse text-left text-sm">
        <caption className="sr-only">Division {number} standings</caption>
        <thead className="text-xs text-fg-muted">
          <tr>
            <th scope="col" className="w-8 py-1 font-medium">
              <span className="sr-only">Place</span>
            </th>
            <th scope="col" className="py-1 font-medium">
              Team
            </th>
            <th scope="col" className="w-7 py-1 text-center font-medium">
              <abbr title="Played">P</abbr>
            </th>
            <th scope="col" className="w-7 py-1 text-center font-medium">
              <abbr title="Won">W</abbr>
            </th>
            <th scope="col" className="w-7 py-1 text-center font-medium">
              <abbr title="Lost">L</abbr>
            </th>
            <th scope="col" className="w-9 py-1 text-center font-medium">
              <abbr title="Games difference">±</abbr>
            </th>
            <th scope="col" className="w-14 py-1 text-right font-medium">
              <span className="sr-only">If the Stage closed now</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {standings.map((standing) => (
            <tr
              key={standing.teamId}
              className={cx("border-t border-border", mine.includes(standing.teamId) ? "font-bold" : "font-medium")}
            >
              <td className="py-1.5 tabular-nums text-fg-muted">
                {results ? `${standing.tied ? "=" : ""}${Number(standing.rank)}` : "–"}
              </td>
              <th scope="row" className="py-1.5 pr-2 font-[inherit] break-words">
                {names[standing.teamId] ?? ""}
              </th>
              <td className="py-1.5 text-center tabular-nums">{Number(standing.played)}</td>
              <td className="py-1.5 text-center tabular-nums">{Number(standing.won)}</td>
              <td className="py-1.5 text-center tabular-nums">{Number(standing.lost)}</td>
              <td className="py-1.5 text-center tabular-nums">{difference(standing)}</td>
              <td className="py-1.5 text-right text-xs">
                <Move standing={standing} results={results} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="text-xs text-fg-muted">
        {results
          ? "Ranked by wins, then games difference, then the results between them. Arrows show who would move if the Stage closed now."
          : "No scores yet: the table fills in as games are played."}
      </p>
    </div>
  );
}
