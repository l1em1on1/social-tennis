"use client";

import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { SubmitButton } from "@/components/ui/submit-button";
import { startLeague } from "@/lib/actions";
import { formatDates, gamesIn } from "@/lib/league";

export interface StartCompetitor {
  teamId: string;
  /** Its Players' names: one in singles, two in doubles. */
  names: string[];
}

/** `count` split into `divisions` Divisions as evenly as possible, the bigger ones at the bottom. */
function split(count: number, divisions: number): number[] {
  const parts = Math.min(Math.max(divisions, 1), Math.floor(count / 2));
  if (parts < 1) return [];
  return Array.from({ length: parts }, (_, index) => Math.floor(count / parts) + (index >= parts - (count % parts) ? 1 : 0));
}

/**
 * The Manager's Start screen (#7, wireframe M4): the proposed starting ladder,
 * top first, in Divisions of about the League's size (ADR-0031). The Manager
 * moves Teams up and down with buttons (easier with a thumb or a screen
 * reader than dragging), across a Division's line too; moves the line between
 * two Divisions to make one a Team bigger; changes how many Divisions there
 * are; and leaves Teams out. Every pair in a Division plays once a Stage, so
 * each Division shows its Games. Confirming sends the ladder, the Division
 * sizes, who is left out and who is removed, as shown here.
 */
export function LeagueStartForm({
  leagueId,
  competitors,
  divisions: proposed,
  unpaired,
  waitingRequestCount,
  firstStage,
}: {
  leagueId: string;
  competitors: StartCompetitor[];
  /** The proposed Division sizes, top first. */
  divisions: number[];
  /** Doubles Players without a partner: starting removes them (owner, 2026-11-20). */
  unpaired: { playerId: string; name: string }[];
  waitingRequestCount: number;
  firstStage: { startDate: string; endDate: string };
}) {
  const [ladder, setLadder] = useState(() => competitors.map((competitor) => competitor.teamId));
  const [leftOut, setLeftOut] = useState<string[]>([]);
  const [sizes, setSizes] = useState(proposed);
  const [announcement, setAnnouncement] = useState("");
  // The control to keep focus on once the list re-renders, and its partner for when it can't go further.
  const refocus = useRef<{ id: string; fallback: string } | null>(null);

  const nameOf = (teamId: string) => competitors.find((competitor) => competitor.teamId === teamId)?.names.join(" & ") ?? "";
  const starts = sizes.map((_, index) => sizes.slice(0, index).reduce((sum, size) => sum + size, 0));
  const divisionOf = (index: number) => starts.filter((start) => start <= index).length;
  const canStart = ladder.length >= 2 && sizes.length > 0 && sizes.every((size) => size >= 2);

  useEffect(() => {
    if (!refocus.current) return;
    const { id, fallback } = refocus.current;
    refocus.current = null;
    const control = document.getElementById(id) as HTMLButtonElement | null;
    (control && !control.disabled ? control : document.getElementById(fallback))?.focus();
  }, [ladder, sizes]);

  function move(index: number, direction: "up" | "down") {
    const target = direction === "up" ? index - 1 : index + 1;
    const next = [...ladder];
    [next[index], next[target]] = [next[target], next[index]];
    const teamId = ladder[index];
    refocus.current = { id: `move-${direction}-${teamId}`, fallback: `move-${direction === "up" ? "down" : "up"}-${teamId}` };
    setLadder(next);
    setAnnouncement(`${nameOf(teamId)} moved ${direction} to place ${target + 1}, Division ${divisionOf(target)}.`);
  }

  /** Moves the line under Division `division` (1-based) a Team up or down. */
  function moveLine(division: number, direction: "up" | "down") {
    const next = [...sizes];
    next[division - 1] += direction === "down" ? 1 : -1;
    next[division] += direction === "down" ? -1 : 1;
    refocus.current = { id: `line-${direction}-${division}`, fallback: `line-${direction === "up" ? "down" : "up"}-${division}` };
    setSizes(next);
    setAnnouncement(`Division ${division} now has ${next[division - 1]}, Division ${division + 1} has ${next[division]}.`);
  }

  function changeDivisions(by: 1 | -1) {
    const next = split(ladder.length, sizes.length + by);
    refocus.current = { id: by > 0 ? "divisions-more" : "divisions-fewer", fallback: by > 0 ? "divisions-fewer" : "divisions-more" };
    setSizes(next);
    setAnnouncement(`${next.length} ${next.length === 1 ? "Division" : "Divisions"}: ${next.join(", ")}.`);
  }

  // Taking a Team off the ladder or putting one back re-splits it into the same number of Divisions.
  function leaveOut(teamId: string) {
    const next = ladder.filter((id) => id !== teamId);
    setLadder(next);
    setLeftOut([...leftOut, teamId]);
    setSizes(split(next.length, sizes.length || 1));
    setAnnouncement(`${nameOf(teamId)} left out.`);
  }

  function putBack(teamId: string) {
    const next = [...ladder, teamId];
    setLadder(next);
    setLeftOut(leftOut.filter((id) => id !== teamId));
    setSizes(split(next.length, sizes.length || 1));
    setAnnouncement(`${nameOf(teamId)} put back, at the bottom of the ladder.`);
  }

  return (
    <form action={startLeague} className="flex flex-col gap-4">
      <input type="hidden" name="leagueId" value={leagueId} />
      {ladder.map((teamId) => (
        <input key={teamId} type="hidden" name="ladder" value={teamId} />
      ))}
      {sizes.map((size, index) => (
        <input key={index} type="hidden" name="divisions" value={size} />
      ))}
      {leftOut.map((teamId) => (
        <input key={teamId} type="hidden" name="leftOut" value={teamId} />
      ))}
      {unpaired.map((player) => (
        <input key={player.playerId} type="hidden" name="removed" value={player.playerId} />
      ))}

      <div className="flex flex-col gap-3 rounded-2xl border border-border bg-surface p-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-base font-semibold">Starting ladder</h2>
          <div className="flex items-center gap-1">
            <Button
              id="divisions-fewer"
              variant="secondary"
              aria-label="Fewer Divisions"
              disabled={sizes.length <= 1}
              onClick={() => changeDivisions(-1)}
              className="px-3"
            >
              −
            </Button>
            <p className="px-1 text-sm">
              {sizes.length} {sizes.length === 1 ? "Division" : "Divisions"}
            </p>
            <Button
              id="divisions-more"
              variant="secondary"
              aria-label="More Divisions"
              disabled={sizes.length >= Math.floor(ladder.length / 2)}
              onClick={() => changeDivisions(1)}
              className="px-3"
            >
              +
            </Button>
          </div>
        </div>

        {sizes.map((size, division) => (
          <section key={division} aria-labelledby={`division-${division + 1}`} className="flex flex-col gap-1">
            <h3 id={`division-${division + 1}`} className="text-sm font-semibold">
              Division {division + 1}{" "}
              <span className="font-normal text-fg-muted">
                · {size} {size === 1 ? "Team" : "Teams"} · {gamesIn(size)} {gamesIn(size) === 1 ? "Game" : "Games"}
              </span>
            </h3>
            <ol aria-label={`Division ${division + 1}`} className="divide-y divide-border">
              {ladder.slice(starts[division], starts[division] + size).map((teamId, offset) => {
                const index = starts[division] + offset;
                const name = nameOf(teamId);
                return (
                  <li key={teamId} className="flex flex-wrap items-center justify-between gap-2 py-2">
                    <p className="min-w-0 flex-1 font-medium break-words">
                      <span className="text-fg-muted">{index + 1}.</span> {name}
                    </p>
                    <div className="flex gap-1">
                      <Button
                        id={`move-up-${teamId}`}
                        variant="secondary"
                        aria-label={`Move ${name} up`}
                        disabled={index === 0}
                        onClick={() => move(index, "up")}
                        className="px-3"
                      >
                        ▲
                      </Button>
                      <Button
                        id={`move-down-${teamId}`}
                        variant="secondary"
                        aria-label={`Move ${name} down`}
                        disabled={index === ladder.length - 1}
                        onClick={() => move(index, "down")}
                        className="px-3"
                      >
                        ▼
                      </Button>
                      <Button variant="ghost" aria-label={`Leave out ${name}`} onClick={() => leaveOut(teamId)}>
                        Leave out
                      </Button>
                    </div>
                  </li>
                );
              })}
            </ol>
            {division < sizes.length - 1 && (
              <div className="flex flex-wrap items-center gap-1 border-t border-dashed border-border-strong pt-2">
                <span className="text-sm text-fg-muted">Line under Division {division + 1}:</span>
                <Button
                  id={`line-up-${division + 1}`}
                  variant="ghost"
                  disabled={size <= 2}
                  onClick={() => moveLine(division + 1, "up")}
                >
                  Up one
                </Button>
                <Button
                  id={`line-down-${division + 1}`}
                  variant="ghost"
                  disabled={sizes[division + 1] <= 2}
                  onClick={() => moveLine(division + 1, "down")}
                >
                  Down one
                </Button>
              </div>
            )}
          </section>
        ))}

        {leftOut.length > 0 && (
          <section aria-labelledby="left-out" className="flex flex-col gap-1">
            <h3 id="left-out" className="text-sm font-semibold">
              Left out <span className="font-normal text-fg-muted">· in the league, without a place</span>
            </h3>
            <ul aria-label="Left out" className="divide-y divide-border">
              {leftOut.map((teamId) => (
                <li key={teamId} className="flex items-center justify-between gap-2 py-2 text-fg-muted">
                  <p className="min-w-0 flex-1 break-words">{nameOf(teamId)}</p>
                  <Button variant="ghost" aria-label={`Put back ${nameOf(teamId)}`} onClick={() => putBack(teamId)}>
                    Put back
                  </Button>
                </li>
              ))}
            </ul>
          </section>
        )}
        <p role="status" className="sr-only">
          {announcement}
        </p>
      </div>

      <ul className="flex flex-col gap-2 text-sm">
        <li>
          Stage 1 runs <strong>{formatDates(firstStage.startDate, firstStage.endDate)}</strong>. Everyone in a Division plays
          everyone else in it once by then.
        </li>
        {leftOut.length > 0 && (
          <li>Anyone left out stays in the league without a place on the ladder. You can still remove them afterwards.</li>
        )}
        {unpaired.length > 0 && (
          <li className="text-danger">
            Starting removes <strong>{unpaired.map((player) => player.name).join(", ")}</strong> from the league: they have no
            partner. They can ask to join again.
          </li>
        )}
        {waitingRequestCount > 0 && (
          <li>
            {waitingRequestCount === 1 ? "1 join request is" : `${waitingRequestCount} join requests are`} still waiting. They
            don&apos;t hold up the start: they stay as reserves, and anyone you approve later joins without a place on the
            ladder.
          </li>
        )}
      </ul>

      <SubmitButton fullWidth disabled={!canStart} pendingLabel="Starting…">
        Confirm and start
      </SubmitButton>
      <p className="text-sm text-fg-muted">
        {canStart
          ? "Starting makes Stage 1's Games. After that, the ladder only changes as results move people."
          : "A ladder needs at least two on it, and every Division at least two."}
      </p>
    </form>
  );
}
