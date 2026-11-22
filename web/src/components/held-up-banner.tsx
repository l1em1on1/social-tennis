import { formatDate } from "@/lib/league";

/**
 * A Stage past its deadline and still open (#10; wireframe P5, "held up"):
 * what it waits on, named, so the Players see what the Manager does. Not a
 * live region: it says how things stand when the page loads.
 */
export function HeldUpBanner({
  stageNumber,
  endDate,
  unscored,
  ties,
}: {
  stageNumber: number;
  endDate: string;
  /** Each Game with no Score, and its Sides: "Una v Vic". */
  unscored: { gameId: string; label: string }[];
  /**
   * Each tie that decides who moves: its Division, and its Teams' ids and
   * names. A middle Division can have two, for first and for last.
   */
  ties: { division: number; teamIds: string[]; names: string[] }[];
}) {
  const waiting = unscored.length > 0 || ties.length > 0;
  return (
    <section aria-label="Stage held up" className="flex flex-col gap-1 rounded-2xl border border-warning bg-warning-muted px-4 py-3 text-sm">
      <p className="font-semibold">
        Stage {stageNumber} ended on {formatDate(endDate)}
        {waiting ? " and is held up" : " and is waiting to close"}
      </p>
      {waiting ? (
        <>
          <ul className="list-disc pl-5">
            {unscored.map((game) => (
              <li key={game.gameId}>No score yet: {game.label}</li>
            ))}
            {ties.map((tie) => (
              <li key={`${tie.division}:${tie.teamIds.join()}`}>
                A tie in Division {tie.division}: {tie.names.join(", ")}
              </li>
            ))}
          </ul>
          <p className="text-fg-muted">The manager settles these, then closes the Stage, and nobody moves before then.</p>
        </>
      ) : (
        <p className="text-fg-muted">Everything is settled: the manager closes it next.</p>
      )}
    </section>
  );
}
