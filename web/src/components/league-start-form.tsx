"use client";

import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { IconButton } from "@/components/ui/icon-button";
import { SubmitButton } from "@/components/ui/submit-button";
import { startLeague } from "@/lib/actions";
import { formatDates, gamesIn } from "@/lib/league";

export interface StartCompetitor {
  teamId: string;
  /** Its Players' names: one in singles, two in doubles. */
  names: string[];
}

/** A Foursomes ladder (#14): as many Divisions of exactly 4 as `count` fills. */
const fours = (count: number) => Array.from({ length: Math.floor(count / 4) }, () => 4);

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
 *
 * A Foursomes ladder (#14, wireframe M4) is Divisions of exactly 4 Players,
 * with no Bye: there is no line to move or number of Divisions to change.
 * Players below the last full Division are listed apart, with how many to
 * leave out or add, and Confirm waits until the count is a multiple of 4.
 */
export function LeagueStartForm({
  leagueId,
  competitors,
  divisions: proposed,
  unpaired,
  waitingRequestCount,
  firstStage,
  foursomes = false,
}: {
  leagueId: string;
  competitors: StartCompetitor[];
  /** The proposed Division sizes, top first. */
  divisions: number[];
  /** Doubles Players without a partner: starting removes them (owner, 2026-11-20). */
  unpaired: { playerId: string; name: string }[];
  waitingRequestCount: number;
  firstStage: { startDate: string; endDate: string };
  /** A Foursomes League: Divisions of exactly 4 Players. */
  foursomes?: boolean;
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
  const placed = sizes.reduce((sum, size) => sum + size, 0);
  // Foursomes only: Players below the last full Division of 4, to leave out or make up to 4.
  const spare = foursomes ? ladder.slice(placed) : [];
  const canStart = foursomes
    ? ladder.length >= 4 && spare.length === 0
    : ladder.length >= 2 && sizes.length > 0 && sizes.every((size) => size >= 2);
  const resize = (count: number) => (foursomes ? fours(count) : split(count, sizes.length || 1));

  useEffect(() => {
    if (!refocus.current) return;
    const { id, fallback } = refocus.current;
    refocus.current = null;
    const control = document.getElementById(id) as HTMLButtonElement | null;
    (control && !control.disabled ? control : document.getElementById(fallback))?.focus();
  }, [ladder, sizes, leftOut]);

  function move(index: number, direction: "up" | "down") {
    const target = direction === "up" ? index - 1 : index + 1;
    const next = [...ladder];
    [next[index], next[target]] = [next[target], next[index]];
    const teamId = ladder[index];
    refocus.current = { id: `move-${direction}-${teamId}`, fallback: `move-${direction === "up" ? "down" : "up"}-${teamId}` };
    setLadder(next);
    const where = target >= placed ? "not in a Division" : `Division ${divisionOf(target)}`;
    setAnnouncement(`${nameOf(teamId)} moved ${direction} to place ${target + 1}, ${where}.`);
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
    // Its row leaves the ladder: focus follows it to its Put back button.
    refocus.current = { id: `put-back-${teamId}`, fallback: `put-back-${teamId}` };
    setLadder(next);
    setLeftOut([...leftOut, teamId]);
    setSizes(resize(next.length));
    setAnnouncement(`${nameOf(teamId)} left out.`);
  }

  function putBack(teamId: string) {
    const next = [...ladder, teamId];
    // Back at the bottom of the ladder: focus follows it there, to Move up, or to
    // Leave out when the ladder is still too short for a Division (no move buttons).
    refocus.current = { id: `move-up-${teamId}`, fallback: `leave-out-${teamId}` };
    setLadder(next);
    setLeftOut(leftOut.filter((id) => id !== teamId));
    setSizes(resize(next.length));
    setAnnouncement(`${nameOf(teamId)} put back, at the bottom of the ladder.`);
  }

  /** A place on the ladder: its number, who, and the buttons to move them or leave them out. */
  function row(teamId: string, index: number) {
    const name = nameOf(teamId);
    return (
      <li key={teamId} className="flex items-center justify-between gap-2 py-1">
        <p className="min-w-0 flex-1 font-medium break-words">
          <span className="text-fg-muted">{index + 1}.</span> {name}
        </p>
        <div className="flex">
          <IconButton
            id={`move-up-${teamId}`}
            icon="up"
            label={`Move ${name} up`}
            disabled={index === 0}
            onClick={() => move(index, "up")}
          />
          <IconButton
            id={`move-down-${teamId}`}
            icon="down"
            label={`Move ${name} down`}
            disabled={index === ladder.length - 1}
            onClick={() => move(index, "down")}
          />
          <IconButton
            id={`leave-out-${teamId}`}
            icon="personMinus"
            word="Leave out"
            className="w-16"
            label={`Leave out ${name}`}
            onClick={() => leaveOut(teamId)}
          />
        </div>
      </li>
    );
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

      {/* A card, its title on the court with the number of Divisions beside it (#254). */}
      <Card
        title="Starting ladder"
        aside={
          foursomes ? undefined : (
            <div className="flex shrink-0 items-center gap-1">
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
          )
        }
      >
        <div className="flex flex-col gap-3">
        {sizes.map((size, division) => (
          <section key={division} aria-labelledby={`division-${division + 1}`} className="flex flex-col gap-1">
            <h3 id={`division-${division + 1}`} className="text-sm font-semibold">
              Division {division + 1}{" "}
              <span className="font-normal text-fg-muted">
                {foursomes
                  ? "· 4 Players · 3 Games"
                  : `· ${size} ${size === 1 ? "Team" : "Teams"} · ${gamesIn(size)} ${gamesIn(size) === 1 ? "Game" : "Games"}`}
              </span>
            </h3>
            <ol aria-label={`Division ${division + 1}`} className="divide-y divide-border">
              {ladder.slice(starts[division], starts[division] + size).map((teamId, offset) => row(teamId, starts[division] + offset))}
            </ol>
            {division < sizes.length - 1 && !foursomes && (
              // The line between two Divisions, on one row: arrows move it, as they move a Team,
              // in the Teams' arrow column, with its label where their Leave out sits.
              <div className="flex items-center">
                <span aria-hidden="true" className="mr-2 flex-1 border-t border-dashed border-border-strong" />
                <IconButton
                  id={`line-up-${division + 1}`}
                  icon="up"
                  label={`Move the line between Division ${division + 1} and ${division + 2} up`}
                  disabled={size <= 2}
                  onClick={() => moveLine(division + 1, "up")}
                />
                <IconButton
                  id={`line-down-${division + 1}`}
                  icon="down"
                  label={`Move the line between Division ${division + 1} and ${division + 2} down`}
                  disabled={sizes[division + 1] <= 2}
                  onClick={() => moveLine(division + 1, "down")}
                />
                <span className="w-16 text-center text-[0.6875rem] whitespace-nowrap text-fg-muted">Move line</span>
              </div>
            )}
          </section>
        ))}

        {spare.length > 0 && (
          // Foursomes: below the last full Division of 4, until someone is left out or more join.
          <section aria-labelledby="spare" className="flex flex-col gap-1">
            <h3 id="spare" className="text-sm font-semibold">
              Not in a Division{" "}
              <span className="font-normal text-fg-muted">
                · {ladder.length >= 4 ? `leave out ${spare.length}, or add ${4 - spare.length} more` : `add ${4 - spare.length} more`}
              </span>
            </h3>
            <ol aria-label="Not in a Division" className="divide-y divide-border">
              {spare.map((teamId, offset) => row(teamId, placed + offset))}
            </ol>
          </section>
        )}

        {sizes.length === 0 && ladder.length > 0 && !foursomes && (
          // Too few for a Division: the Teams still on the ladder stay in view, to leave out or add to.
          <section aria-labelledby="too-few" className="flex flex-col gap-1">
            <h3 id="too-few" className="text-sm font-semibold">
              On the ladder <span className="font-normal text-fg-muted">· a Division needs at least two</span>
            </h3>
            <ol aria-label="On the ladder" className="divide-y divide-border">
              {ladder.map((teamId) => (
                <li key={teamId} className="flex items-center justify-between gap-2 py-1">
                  <p className="min-w-0 flex-1 font-medium break-words">{nameOf(teamId)}</p>
                  <IconButton
                    id={`leave-out-${teamId}`}
                    icon="personMinus"
                    word="Leave out"
                    label={`Leave out ${nameOf(teamId)}`}
                    onClick={() => leaveOut(teamId)}
                  />
                </li>
              ))}
            </ol>
          </section>
        )}

        {leftOut.length > 0 && (
          <section aria-labelledby="left-out" className="flex flex-col gap-1">
            <h3 id="left-out" className="text-sm font-semibold">
              Left out <span className="font-normal text-fg-muted">· in the league, without a place</span>
            </h3>
            <ul aria-label="Left out" className="divide-y divide-border">
              {leftOut.map((teamId) => (
                <li key={teamId} className="flex items-center justify-between gap-2 py-1 text-fg-muted">
                  <p className="min-w-0 flex-1 break-words">{nameOf(teamId)}</p>
                  <IconButton
                    id={`put-back-${teamId}`}
                    icon="personPlus"
                    word="Put back"
                    label={`Put back ${nameOf(teamId)}`}
                    onClick={() => putBack(teamId)}
                  />
                </li>
              ))}
            </ul>
          </section>
        )}
        <p role="status" className="sr-only">
          {announcement}
        </p>
        </div>
      </Card>

      <ul className="flex flex-col gap-2 text-sm">
        <li>
          Stage 1 runs <strong>{formatDates(firstStage.startDate, firstStage.endDate)}</strong>.{" "}
          {foursomes
            ? "Each Division plays 3 games by then, everyone partnering each of the others once."
            : "Everyone in a Division plays everyone else in it once by then."}
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
          : foursomes
            ? "A Foursomes ladder is Divisions of 4 players, with no one sitting out: it needs a multiple of 4."
            : "A ladder needs at least two on it, and every Division at least two."}
      </p>
    </form>
  );
}
