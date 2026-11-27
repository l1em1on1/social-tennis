import type { components } from "@/lib/api/schema";
import { cx } from "@/lib/cx";
import { STANDINGS_MODES } from "@/lib/league";

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
 * would go if the Stage closed now, and in a Bonus points League (#214) its
 * points, which rank it. Teams the tie-breaks can't separate
 * share a rank (=2); where that decides who moves, it says "tied", for the
 * Manager to settle at close. Before any Score, it says so instead.
 *
 * A Foursomes Division (#14, ADR-0036) lists its 4 Players with the Games
 * they played and the games they banked, which alone rank them; Players
 * level where it decides a move are the Manager's to order, and a Game not
 * played leaves the whole Division to the Manager.
 */
export function StandingsTable({
  number,
  standings,
  names,
  mine = [],
  foursomes,
}: {
  number: number;
  standings: Standing[];
  /** A Foursomes Division: ranked by games banked, and whether one of its Games wasn't played. */
  foursomes?: { gameNotPlayed: boolean };
  /** Each Team's name, by Team id. */
  names: Record<string, string>;
  /** The Acting Player's Team, shown in bold. */
  mine?: string[];
}) {
  const results = standings.some((standing) => Number(standing.played) > 0);
  // The API sends points only where they rank (StandingsMode.BonusPoints). Their column fits a
  // phone because the numbers' columns are only as wide as their figures (one digit for P, W, L).
  const points = standings.some((standing) => standing.points != null);
  const rule = foursomes
    ? foursomes.gameNotPlayed
      ? "A game wasn't played, so the Manager decides who moves."
      : "Ranked by games banked: the games your side won in each game. Players level for a place that moves are the Manager's to order. The middle two stay."
    : STANDINGS_MODES.find((mode) => mode.value === (points ? "BonusPoints" : "Standard"))!.rule;
  return (
    <div className="flex flex-col gap-2">
      <table className="w-full border-collapse text-left text-sm">
        <caption className="sr-only">Division {number} standings</caption>
        <thead className="text-xs text-fg-muted">
          <tr>
            <th scope="col" className="w-6 py-1 font-medium">
              <span className="sr-only">Place</span>
            </th>
            <th scope="col" className="py-1 font-medium">
              {foursomes ? "Player" : "Team"}
            </th>
            <th scope="col" className="w-5 py-1 text-center font-medium">
              <abbr title="Played">P</abbr>
            </th>
            {foursomes ? (
              <th scope="col" className="w-14 py-1 text-center font-medium">
                <abbr title="Games banked">Games</abbr>
              </th>
            ) : (
              <>
                <th scope="col" className="w-5 py-1 text-center font-medium">
                  <abbr title="Won">W</abbr>
                </th>
                <th scope="col" className="w-5 py-1 text-center font-medium">
                  <abbr title="Lost">L</abbr>
                </th>
                <th scope="col" className="w-7 py-1 text-center font-medium">
                  <abbr title="Games difference">±</abbr>
                </th>
              </>
            )}
            {points && (
              <th scope="col" className="w-7 py-1 text-center font-medium">
                <abbr title="Points">Pts</abbr>
              </th>
            )}
            <th scope="col" className="w-12 py-1 text-right font-medium">
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
              <th scope="row" className="py-1.5 pr-2 font-[weight:inherit] wrap-anywhere">
                {names[standing.teamId] ?? ""}
              </th>
              <td className="py-1.5 text-center tabular-nums">{Number(standing.played)}</td>
              {foursomes ? (
                <td className="py-1.5 text-center tabular-nums">{Number(standing.gamesFor)}</td>
              ) : (
                <>
                  <td className="py-1.5 text-center tabular-nums">{Number(standing.won)}</td>
                  <td className="py-1.5 text-center tabular-nums">{Number(standing.lost)}</td>
                  <td className="py-1.5 text-center tabular-nums">{difference(standing)}</td>
                </>
              )}
              {points && <td className="py-1.5 text-center tabular-nums">{Number(standing.points ?? 0)}</td>}
              <td className="py-1.5 text-right text-xs">
                <Move standing={standing} results={results} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="text-xs text-fg-muted">
        {results
          ? `${rule} Arrows show who would move if the Stage closed now.`
          : "No scores yet: the table fills in as games are played."}
      </p>
    </div>
  );
}
