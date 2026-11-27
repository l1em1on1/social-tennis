import { SubmitButton } from "@/components/ui/submit-button";
import { saveAvailability } from "@/lib/actions";

const DAY_MS = 24 * 60 * 60 * 1000;
const WEEKDAYS = [
  ["M", "Monday"],
  ["T", "Tuesday"],
  ["W", "Wednesday"],
  ["T", "Thursday"],
  ["F", "Friday"],
  ["S", "Saturday"],
  ["S", "Sunday"],
] as const;

/** A calendar date as the API writes it, read in UTC so no zone moves it a day (ADR-0016). */
const asDate = (day: string) => new Date(`${day}T00:00:00Z`);
const asDay = (date: Date) => date.toISOString().slice(0, 10);
const say = (date: Date, options: Intl.DateTimeFormatOptions) => date.toLocaleDateString("en-GB", { ...options, timeZone: "UTC" });

/** The days from `first` to `last`, each month apart, as the calendar shows them. */
function months(first: string, last: string): { name: string; days: Date[] }[] {
  const result: { name: string; days: Date[] }[] = [];
  for (let time = asDate(first).getTime(); time <= asDate(last).getTime(); time += DAY_MS) {
    const date = new Date(time);
    const name = say(date, { month: "long", year: "numeric" });
    if (result.at(-1)?.name !== name) result.push({ name, days: [] });
    result.at(-1)!.days.push(date);
  }
  return result;
}

/**
 * The days a Player is free in their League's current Stage (#9; owner,
 * 2026-11-27: whole days), as a calendar of tick boxes from the Stage's start
 * to the last day it takes, past days included: a past tick does no harm, as
 * shared days start from today. Saving sends every ticked day, replacing the
 * last list. Real checkboxes, so it works without script and each day reads
 * as its full date.
 */
export function AvailabilityForm({
  leagueId,
  playerId,
  firstDay,
  lastDay,
  dates,
}: {
  leagueId: string;
  /** The Acting Player. */
  playerId: string;
  /** The first day shown: the Stage's start. */
  firstDay: string;
  /** The last day the Stage takes (the API's `lastDay`). */
  lastDay: string;
  /** The days already ticked. */
  dates: string[];
}) {
  const ticked = new Set(dates);
  return (
    <form action={saveAvailability} className="flex flex-col gap-4">
      <input type="hidden" name="leagueId" value={leagueId} />
      <input type="hidden" name="playerId" value={playerId} />
      {months(firstDay, lastDay).map((month) => (
        <fieldset key={month.name} className="flex flex-col gap-1">
          <legend className="mb-2 font-semibold">{month.name}</legend>
          <div aria-hidden="true" className="grid grid-cols-7 text-center text-xs text-fg-muted">
            {WEEKDAYS.map(([letter, name]) => (
              <abbr key={name} title={name} className="no-underline">
                {letter}
              </abbr>
            ))}
          </div>
          <div className="grid grid-cols-7 gap-1">
            {/* Monday first: a month starting later in the week leaves gaps before it. */}
            {Array.from({ length: (month.days[0].getUTCDay() + 6) % 7 }, (_, gap) => (
              <span key={`gap-${gap}`} />
            ))}
            {month.days.map((date) => {
              const day = asDay(date);
              return (
                <label
                  key={day}
                  className="flex h-11 cursor-pointer items-center justify-center rounded-md border border-border text-sm font-medium tabular-nums has-[:checked]:border-accent has-[:checked]:bg-accent has-[:checked]:text-accent-fg has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-accent"
                >
                  <input
                    type="checkbox"
                    name="dates"
                    value={day}
                    defaultChecked={ticked.has(day)}
                    aria-label={say(date, { weekday: "long", day: "numeric", month: "long" })}
                    className="sr-only"
                  />
                  {date.getUTCDate()}
                </label>
              );
            })}
          </div>
        </fieldset>
      ))}
      <SubmitButton fullWidth pendingLabel="Saving…">
        Save free days
      </SubmitButton>
    </form>
  );
}
