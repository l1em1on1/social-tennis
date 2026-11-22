import type { components } from "@/lib/api/schema";
import { formatDate } from "@/lib/league";
import { formatInstant, formatSets } from "@/lib/score";

type Entry = components["schemas"]["ScoreHistoryEntry"];
type Snapshot = components["schemas"]["ScoreSnapshot"];

const DID: Record<Entry["kind"], string> = {
  Entered: "entered it",
  Corrected: "corrected it",
  Overridden: "overrode it",
  Recorded: "recorded it",
};

/** Who made a change: the Player, or for a Manager's change their own Player, or just "A manager". */
function who(entry: Entry): string {
  if (entry.kind !== "Overridden" && entry.kind !== "Recorded") {
    return entry.by ?? "Someone";
  }
  return entry.by ? `${entry.by} (manager)` : "A manager";
}

/** A Score in a line: its Sets, who won, a Retirement, and the day if it moved. */
function describe(snapshot: Snapshot, sides: [string, string], playedOn?: string): string {
  const winner = sides[Number(snapshot.winner) - 1];
  return [
    formatSets(snapshot.sets),
    snapshot.retired ? `ret., ${winner} won` : `${winner} won`,
    playedOn ? `played ${formatDate(playedOn)}` : null,
  ]
    .filter(Boolean)
    .join(" · ");
}

/**
 * A Score's history (#38, wireframe P3): every change, the latest first, with
 * who made it, when (in the Club's time zone), and from what to what. In the
 * first release it stands in for voting (ADR-0008, Phasing).
 */
export function ScoreHistory({ history, sides, timeZone }: { history: Entry[]; sides: [string, string]; timeZone: string }) {
  return (
    <ol aria-label="Changes to the score" className="flex flex-col gap-3 border-l-2 border-border pl-4">
      {history.map((entry, index) => {
        // The day shows only where it changed.
        const moved = entry.before && entry.before.playedOn !== entry.after.playedOn;
        return (
          <li key={index} className="flex flex-col gap-1">
            <p className="text-sm">
              <strong>{who(entry)}</strong> {DID[entry.kind]}{" "}
              <span className="text-fg-muted">· {formatInstant(entry.at, timeZone)}</span>
            </p>
            <p className="font-mono text-sm break-words">
              {entry.before && (
                <>
                  <del className="text-fg-muted">{describe(entry.before, sides, moved ? entry.before.playedOn : undefined)}</del>{" "}
                  <span aria-hidden="true">→</span> <span className="sr-only">to</span>{" "}
                </>
              )}
              <ins className="no-underline">{describe(entry.after, sides, moved ? entry.after.playedOn : undefined)}</ins>
            </p>
          </li>
        );
      })}
    </ol>
  );
}
