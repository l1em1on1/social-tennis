import Link from "next/link";
import { Card } from "@/components/ui/card";
import { Field, Input } from "@/components/ui/field";
import { SubmitButton } from "@/components/ui/submit-button";
import { arrangeGame } from "@/lib/actions";
import type { components } from "@/lib/api/schema";

type Availability = components["schemas"]["GameAvailability"];

const list = (items: string[]) => new Intl.ListFormat("en-GB").format(items);

/** A whole day in words, read in UTC so no zone moves it (ADR-0016): "Sat 20 Jul". */
export const shortDay = (day: string) =>
  new Date(`${day}T00:00:00Z`).toLocaleDateString("en-GB", { weekday: "short", day: "numeric", month: "short", timeZone: "UTC" });

/** "Agreed for Sat 20 Jul · 6 - astro": a Game's agreed Date and Court, for anyone who can see it (#9). */
export function describeArrangement(date: string, court?: string | null) {
  return `Agreed for ${shortDay(date)}${court ? ` · ${court}` : ""}`;
}

/**
 * When a Game is played (#9, owner 2026-11-27), for its own Players while it
 * has no score: the days that suit everyone in it, who hasn't said yet, and
 * the agreed Date and Court, which any of them sets and the last change
 * stands. Their own free days are a page of their own.
 */
export function GameArrangement({
  gameId,
  leagueId,
  playerId,
  availability,
  date,
  startTime,
  court,
  dateSetBy,
  stageStart,
}: {
  gameId: string;
  leagueId: string;
  /** The Acting Player, who plays in it. */
  playerId: string;
  availability: Availability;
  date?: string | null;
  /** Its start time (#276), kept while the Date stays the same: the form doesn't ask for one. */
  startTime?: string | null;
  court?: string | null;
  dateSetBy?: string | null;
  /** The first day it can be played. */
  stageStart: string;
}) {
  const shared = availability.shared.map((day) => day.date);
  return (
    <Card title="When to play" aria-label="When to play">
      <div className="flex flex-col gap-3 text-sm">
        {date && (
          <p role="note" className="rounded-md border border-accent bg-accent-muted px-3 py-2">
            <strong>{describeArrangement(date, court)}</strong>
            {dateSetBy ? <span className="text-fg-muted">, set by {dateSetBy}</span> : null}
          </p>
        )}
        {date && (
          // A calendar file for the agreed Date (#9 part 3): a plain link, so the phone hands it to its calendar.
          <p>
            <a href={`/games/${gameId}/calendar`} download className="font-medium text-accent underline">
              Add to calendar
            </a>
          </p>
        )}
        <div className="flex flex-col gap-1">
          <h3 className="font-semibold">Days that suit everyone</h3>
          {shared.length > 0 ? (
            <ul aria-label="Days that suit everyone" className="flex flex-wrap gap-2">
              {shared.map((day) => (
                <li key={day} className="rounded-full border border-border px-3 py-1 tabular-nums">
                  {shortDay(day)}
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-fg-muted">
              {availability.notGiven.length > 0
                ? `None yet: waiting for ${list(availability.notGiven)} to say which days they're free.`
                : "None yet: no day suits everyone."}
            </p>
          )}
          <p>
            <Link href={`/leagues/${leagueId}/availability`} className="font-medium text-accent underline">
              Your free days
            </Link>
          </p>
        </div>
        <form action={arrangeGame} className="flex flex-col gap-3 border-t border-border pt-3">
          <input type="hidden" name="gameId" value={gameId} />
          <input type="hidden" name="playerId" value={playerId} />
          {date && startTime && (
            <>
              <input type="hidden" name="agreedDate" value={date} />
              <input type="hidden" name="startTime" value={startTime} />
            </>
          )}
          <Field id="game-date" label="Agreed date" hint="Any of you can set it or change it. Leave it empty to clear it.">
            {(control) => <Input {...control} name="date" type="date" min={stageStart} defaultValue={date ?? shared[0] ?? ""} />}
          </Field>
          <Field id="game-court" label="Court (optional)" hint="For yourselves, e.g. 6 - astro.">
            {(control) => <Input {...control} name="court" maxLength={50} autoComplete="off" defaultValue={court ?? ""} />}
          </Field>
          <div>
            <SubmitButton pendingLabel="Saving…">{date ? "Change" : "Save"}</SubmitButton>
          </div>
        </form>
      </div>
    </Card>
  );
}
