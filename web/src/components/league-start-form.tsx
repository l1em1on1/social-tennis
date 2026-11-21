"use client";

import { useEffect, useRef, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { SubmitButton } from "@/components/ui/submit-button";
import { startLeague } from "@/lib/actions";
import { cx } from "@/lib/cx";
import { divisionOf, formatDates } from "@/lib/league";

export interface StartCompetitor {
  teamId: string;
  /** Its Players' names: one in singles, two in doubles. */
  names: string[];
}

/**
 * The Manager's Start screen (#7, wireframe M4): the proposed starting ladder,
 * top first, to reorder with up and down buttons (easier with a thumb or a
 * screen reader than dragging) and to leave Competitors out of. Each row shows
 * the Division it would start in, or the Bye, as it changes. Confirming sends
 * the order, who is left out and who is removed, as shown here.
 */
export function LeagueStartForm({
  leagueId,
  competitors,
  unpaired,
  waitingRequestCount,
  firstStage,
}: {
  leagueId: string;
  competitors: StartCompetitor[];
  /** Doubles Players without a partner: starting removes them (owner, 2026-11-20). */
  unpaired: { playerId: string; name: string }[];
  waitingRequestCount: number;
  firstStage: { startDate: string; endDate: string };
}) {
  const [order, setOrder] = useState(() => competitors.map((competitor) => competitor.teamId));
  const [out, setOut] = useState<ReadonlySet<string>>(() => new Set());
  const [announcement, setAnnouncement] = useState("");
  const moved = useRef<{ teamId: string; direction: "up" | "down" } | null>(null);

  const nameOf = (teamId: string) => competitors.find((competitor) => competitor.teamId === teamId)?.names.join(" & ") ?? "";
  const ladder = order.filter((teamId) => !out.has(teamId));
  const placeOf = (teamId: string) => ladder.indexOf(teamId) + 1;
  const divisions = Math.floor(ladder.length / 2);
  const bye = ladder.length % 2 === 1 ? ladder[ladder.length - 1] : undefined;

  // Moving a row re-renders the list: keep focus on the button that moved it,
  // or on its partner button once it can't go further that way.
  useEffect(() => {
    if (!moved.current) return;
    const { teamId, direction } = moved.current;
    moved.current = null;
    const button = document.getElementById(`move-${direction}-${teamId}`) as HTMLButtonElement | null;
    const fallback = document.getElementById(`move-${direction === "up" ? "down" : "up"}-${teamId}`);
    (button && !button.disabled ? button : fallback)?.focus();
  }, [order]);

  function move(index: number, direction: "up" | "down") {
    const target = direction === "up" ? index - 1 : index + 1;
    const next = [...order];
    [next[index], next[target]] = [next[target], next[index]];
    const teamId = order[index];
    moved.current = { teamId, direction };
    setOrder(next);
    // The place on the ladder that would be submitted: left-out rows don't count.
    if (out.has(teamId)) {
      setAnnouncement(`${nameOf(teamId)} moved ${direction}, still left out.`);
    } else {
      const ladderNext = next.filter((id) => !out.has(id));
      const place = ladderNext.indexOf(teamId) + 1;
      const division = divisionOf(place, ladderNext.length);
      setAnnouncement(
        `${nameOf(teamId)} moved ${direction} to place ${place} on the ladder, ${division ? `Division ${division}` : "the Bye"}.`,
      );
    }
  }

  function toggle(teamId: string) {
    const next = new Set(out);
    if (next.has(teamId)) next.delete(teamId);
    else next.add(teamId);
    setOut(next);
    setAnnouncement(`${nameOf(teamId)} ${next.has(teamId) ? "left out" : "put back"}.`);
  }

  return (
    <form action={startLeague} className="flex flex-col gap-4">
      <input type="hidden" name="leagueId" value={leagueId} />
      {ladder.map((teamId) => (
        <input key={teamId} type="hidden" name="ladder" value={teamId} />
      ))}
      {order
        .filter((teamId) => out.has(teamId))
        .map((teamId) => (
          <input key={teamId} type="hidden" name="leftOut" value={teamId} />
        ))}
      {unpaired.map((player) => (
        <input key={player.playerId} type="hidden" name="removed" value={player.playerId} />
      ))}

      <div className="flex flex-col gap-2 rounded-2xl border border-border bg-surface p-4">
        <div className="flex items-baseline justify-between gap-2">
          <h2 className="text-base font-semibold">Starting ladder</h2>
          <p className="text-sm text-fg-muted">
            {ladder.length} on it · {divisions} {divisions === 1 ? "Division" : "Divisions"}
          </p>
        </div>
        <ol aria-label="Starting ladder" className="divide-y divide-border">
          {order.map((teamId, index) => {
            const name = nameOf(teamId);
            const left = out.has(teamId);
            const division = left ? null : divisionOf(placeOf(teamId), ladder.length);
            return (
              <li key={teamId} className="flex flex-wrap items-center justify-between gap-2 py-2">
                <div className={cx("min-w-0 flex-1", left && "text-fg-muted")}>
                  <p className={cx("font-medium break-words", left && "line-through")}>{name}</p>
                  <p className="text-sm">
                    {left ? (
                      <Badge>Left out</Badge>
                    ) : division ? (
                      <Badge tone="accent">{`Division ${division}`}</Badge>
                    ) : (
                      <Badge tone="info">Bye</Badge>
                    )}
                  </p>
                </div>
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
                    disabled={index === order.length - 1}
                    onClick={() => move(index, "down")}
                    className="px-3"
                  >
                    ▼
                  </Button>
                  <Button variant="ghost" aria-label={`${left ? "Put back" : "Leave out"} ${name}`} onClick={() => toggle(teamId)}>
                    {left ? "Put back" : "Leave out"}
                  </Button>
                </div>
              </li>
            );
          })}
        </ol>
        <p role="status" className="sr-only">
          {announcement}
        </p>
      </div>

      <ul className="flex flex-col gap-2 text-sm">
        <li>
          Stage 1 runs <strong>{formatDates(firstStage.startDate, firstStage.endDate)}</strong>. Each Division plays its Game by
          then.
        </li>
        {bye && (
          <li>
            <strong>{nameOf(bye)}</strong> has the Bye: with an odd number, the last on the ladder sits out Stage 1 and doesn&apos;t
            move.
          </li>
        )}
        {out.size > 0 && (
          <li>Anyone left out stays in the league without a place on the ladder. You can still remove them afterwards.</li>
        )}
        {unpaired.length > 0 && (
          <li className="text-danger">
            Starting removes{" "}
            <strong>{unpaired.map((player) => player.name).join(", ")}</strong> from the league: they have no partner. They can ask
            to join again.
          </li>
        )}
        {waitingRequestCount > 0 && (
          <li>
            {waitingRequestCount === 1 ? "1 join request is" : `${waitingRequestCount} join requests are`} still waiting. Anyone
            you approve after the start joins without a place on the ladder.
          </li>
        )}
      </ul>

      <SubmitButton fullWidth disabled={ladder.length < 2} pendingLabel="Starting…">
        Confirm and start
      </SubmitButton>
      <p className="text-sm text-fg-muted">
        {ladder.length < 2
          ? "A ladder needs at least two on it."
          : "Starting makes Stage 1's Games. After that, the ladder only changes as results move people."}
      </p>
    </form>
  );
}
