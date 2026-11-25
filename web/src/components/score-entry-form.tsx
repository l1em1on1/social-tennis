"use client";

import { useActionState, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Field, Input } from "@/components/ui/field";
import { ErrorState } from "@/components/ui/states";
import { Stepper } from "@/components/ui/stepper";
import { SubmitButton } from "@/components/ui/submit-button";
import { submitScore, type SubmitScoreState } from "@/lib/actions";
import { cx } from "@/lib/cx";
import { formatDate } from "@/lib/league";
import {
  atTiebreak,
  checkScore,
  checkSet,
  emptySet,
  formatSet,
  isMatchTiebreak,
  recordedSet,
  setsToSend,
  shownSets,
  type ScoreFormat,
  type SetEntry,
  type Side,
} from "@/lib/score";

/**
 * Entering a Score; a Player who played correcting it while the Stage is
 * open; a Manager overriding it, open or closed (#38); or a Manager
 * recording one for a Game nobody scored (#10).
 */
export type ScoreEntryMode = "enter" | "correct" | "override" | "record";

export interface ScoreEntryFormProps {
  gameId: string;
  mode?: ScoreEntryMode;
  /** The Acting Player, who plays in the Game, entering or correcting it as them. None for an override. */
  playerId?: string;
  /** The Score's version a correction or an override is made from (ADR-0018). */
  version?: number;
  /** Side 1's name, then Side 2's ("Una", "Ann & Bob"). */
  sides: [string, string];
  format: ScoreFormat;
  /** The Stage's first day and today where the Club is: the date played falls between them. */
  stageStart: string;
  today: string;
  /** Where the form starts, for its stories: by default an empty Score played today. */
  initial?: { sets?: SetEntry[]; playedOn?: string; retirementWinner?: Side };
}

/** The most points a tiebreak or a Match Tiebreak's stepper goes to. */
const MAX_POINTS = 30;

const setTitle = (format: ScoreFormat, index: number) =>
  isMatchTiebreak(format, index) ? "Match tiebreak" : format.setsPerGame === 1 ? "Set" : `Set ${index + 1}`;

/**
 * Entering a Game's Score (#38, wireframe P4, layout A): every Set on one
 * screen, two steppers each. The tiebreak points appear at 5–5 (8–8), or
 * at 6–5 either way as recorded; a finished tiebreak at 5–5 is saved as
 * 6–5 to its winner. The third Set appears only at one set all, as a Match
 * Tiebreak where the League plays one. A Retirement keeps the Sets as they stood, with a declared winner.
 *
 * The hints are the API's own rules (`lib/score.ts`); the API still decides,
 * and anything it refuses shows where it belongs.
 */
const SAVE: Record<ScoreEntryMode, string> = {
  enter: "Save score",
  correct: "Save correction",
  override: "Save override",
  record: "Save score",
};

/** What the entry would save, to tell a change from none. */
const entryKey = (format: ScoreFormat, entries: SetEntry[], playedOn: string, retirementWinner: Side | null) =>
  JSON.stringify({ sets: setsToSend(format, entries), playedOn, retirementWinner });

export function ScoreEntryForm({ gameId, mode = "enter", playerId, version, sides, format, stageStart, today, initial }: ScoreEntryFormProps) {
  const [state, action] = useActionState<SubmitScoreState, FormData>(submitScore, { errors: {} });
  const [entries, setEntries] = useState<SetEntry[]>(() =>
    [0, 1, 2].map((index) => initial?.sets?.[index] ?? emptySet()),
  );
  const [playedOn, setPlayedOn] = useState(initial?.playedOn ?? today);
  const [retired, setRetired] = useState(initial?.retirementWinner !== undefined);
  const [retirementWinner, setRetirementWinner] = useState<Side | null>(initial?.retirementWinner ?? null);
  // A correction or override saves only a change (#213 review): what it started from, to compare.
  const [startedFrom] = useState(() =>
    mode === "enter" || mode === "record" ? null : entryKey(format, entries, playedOn, retired ? retirementWinner : null),
  );

  const shown = shownSets(format, entries);
  const sets = setsToSend(format, entries);
  const check = checkScore(format, sets, retired ? retirementWinner : null);
  const unchanged = startedFrom === entryKey(format, entries, playedOn, retired ? retirementWinner : null);
  const dateOk = playedOn >= stageStart && playedOn <= today;
  const summary = sets
    .map((set, index) => ({ set, index }))
    .filter(({ set }) => set.first > 0 || set.second > 0)
    .map(({ set, index }) => formatSet({ ...set, matchTiebreak: isMatchTiebreak(format, index) }))
    .join(" ");

  const change = (index: number, patch: Partial<SetEntry>) =>
    setEntries((current) => current.map((entry, at) => (at === index ? { ...entry, ...patch } : entry)));

  const verdict = retired && retirementWinner === null
    ? "Pick who won the game that stopped early."
    : unchanged
      ? `Change something to save ${mode === "override" ? "an override" : "a correction"}.`
      : check.ok
      ? `${sides[check.winner - 1]} wins${retired ? " (retirement)" : ""}`
      : check.message;

  return (
    <form action={action} className="flex flex-col gap-4" noValidate>
      <input type="hidden" name="gameId" value={gameId} />
      <input type="hidden" name="mode" value={mode} />
      {playerId && <input type="hidden" name="playerId" value={playerId} />}
      {version !== undefined && <input type="hidden" name="version" value={version} />}
      {sets.map((set, index) => (
        <span key={index} hidden>
          <input type="hidden" name="first" value={set.first} />
          <input type="hidden" name="second" value={set.second} />
          <input type="hidden" name="firstTiebreak" value={set.firstTiebreak ?? ""} />
          <input type="hidden" name="secondTiebreak" value={set.secondTiebreak ?? ""} />
        </span>
      ))}

      {state.errors.form && <ErrorState title="The score wasn't saved" description={state.errors.form.join(" ")} />}

      <Field
        id="playedOn"
        label="Date played"
        hint={`From ${formatDate(stageStart)} to today, ${formatDate(today)}.`}
        error={state.errors.playedOn?.join(" ") ?? (dateOk ? undefined : "Pick a day from the Stage's start to today.")}
      >
        {(control) => (
          <Input
            {...control}
            type="date"
            name="playedOn"
            min={stageStart}
            max={today}
            value={playedOn}
            onChange={(event) => setPlayedOn(event.target.value)}
            required
          />
        )}
      </Field>

      {entries.slice(0, shown).map((entry, index) => {
        const title = setTitle(format, index);
        const matchTiebreak = isMatchTiebreak(format, index);
        const recorded = recordedSet(format, entry, index);
        const result = checkSet(format, recorded, matchTiebreak);
        const untouched = recorded.first === 0 && recorded.second === 0 && recorded.firstTiebreak === null;
        const serverError = state.errors[`sets[${index}]`]?.join(" ");
        const message = serverError ?? (untouched || result.state === "won" ? null : result.message);
        const max = matchTiebreak ? MAX_POINTS : format.gamesPerSet;
        return (
          <section
            key={index}
            aria-label={title}
            className={cx(
              "flex flex-col gap-4 rounded-2xl border bg-surface p-4",
              result.state === "won" && !serverError ? "border-success" : result.state === "impossible" || serverError ? "border-danger" : "border-border",
            )}
          >
            <header className="flex items-center justify-between gap-3">
              <h2 className="text-base font-semibold">{title}</h2>
              {matchTiebreak && result.state !== "won" && <Badge tone="info">First to 10</Badge>}
              {result.state === "won" && <Badge tone="success">{formatSet({ ...recorded, matchTiebreak })}</Badge>}
            </header>
            {sides.map((name, side) => (
              <div key={side} className="flex items-center justify-between gap-3">
                <span className="min-w-0 break-words font-medium">{name}</span>
                <Stepper
                  label={`${title}, ${name}`}
                  value={side === 0 ? entry.first : entry.second}
                  max={max}
                  onChange={(value) => change(index, side === 0 ? { first: value } : { second: value })}
                />
              </div>
            ))}
            {atTiebreak(format, entry, index) && (
              <div className="flex flex-col gap-4 rounded-xl bg-surface-muted p-3">
                <p className="flex items-center gap-2 text-sm font-medium">
                  Tiebreak points <Badge tone="accent">{`at ${format.gamesPerSet - 1}–${format.gamesPerSet - 1}`}</Badge>
                </p>
                {sides.map((name, side) => (
                  <div key={side} className="flex items-center justify-between gap-3">
                    <span className="min-w-0 break-words text-sm">{name}</span>
                    <Stepper
                      label={`${title} tiebreak, ${name}`}
                      value={side === 0 ? entry.firstPoints : entry.secondPoints}
                      max={MAX_POINTS}
                      onChange={(value) => change(index, side === 0 ? { firstPoints: value } : { secondPoints: value })}
                    />
                  </div>
                ))}
              </div>
            )}
            {message && (
              <p className={cx("text-sm", serverError || result.state === "impossible" ? "font-medium text-danger" : "text-fg-muted")}>
                {message}
              </p>
            )}
          </section>
        );
      })}
      {format.setsPerGame === 3 && shown === 2 && (
        <section
          aria-label={setTitle(format, 2)}
          className="flex items-center justify-between gap-3 rounded-2xl border border-dashed border-border p-4 text-fg-muted"
        >
          <h2 className="text-base font-semibold">{setTitle(format, 2)}</h2>
          <Badge>Only at one set all</Badge>
        </section>
      )}

      <section aria-label="Retirement" className="flex flex-col gap-3 rounded-2xl border border-border bg-surface p-4">
        <label className="flex cursor-pointer items-start justify-between gap-3">
          <span>
            <span className="block font-semibold">Stopped early (retirement)</span>
            <span className="block text-sm text-fg-muted">Injury, rain, court time up. Enter the sets as they stood.</span>
          </span>
          <input
            type="checkbox"
            role="switch"
            className="mt-1 size-6 accent-accent"
            checked={retired}
            onChange={(event) => setRetired(event.target.checked)}
          />
        </label>
        {retired && (
          <fieldset className="flex flex-col gap-2">
            <legend className="mb-1 text-sm font-medium">Who won?</legend>
            <div className="grid grid-cols-2 gap-2">
              {sides.map((name, side) => (
                <label
                  key={side}
                  className={cx(
                    "flex min-h-11 cursor-pointer items-center justify-center rounded-xl border px-3 text-center font-semibold wrap-anywhere",
                    "has-checked:border-accent has-checked:bg-accent-muted has-checked:text-accent",
                    "has-focus-visible:outline-2 has-focus-visible:outline-accent",
                    "border-border-strong",
                  )}
                >
                  <input
                    type="radio"
                    name="retirementWinner"
                    value={side + 1}
                    className="sr-only"
                    checked={retirementWinner === side + 1}
                    onChange={() => setRetirementWinner((side + 1) as Side)}
                  />
                  {name}
                </label>
              ))}
            </div>
          </fieldset>
        )}
      </section>

      <div className="flex flex-col gap-2 rounded-2xl border border-border bg-surface p-4">
        <p className="font-mono text-lg font-semibold" aria-label="Score so far">
          {summary || "—"}
        </p>
        <p
          role="status"
          className={cx("text-sm", check.ok && !unchanged && !(retired && retirementWinner === null) ? "font-semibold text-success" : "text-fg-muted")}
        >
          {verdict}
        </p>
        {(state.errors.sets ?? state.errors.retirementWinner) && (
          <p className="text-sm font-medium text-danger">{(state.errors.sets ?? state.errors.retirementWinner)?.join(" ")}</p>
        )}
        {mode !== "enter" && (
          <p className="text-sm text-fg-muted">
            {mode === "override"
              ? "Your override is recorded as yours, with the score it replaces, and everyone in the league can see it."
              : mode === "record"
                ? "It's recorded as a manager's, and the players in the game can still correct it while the Stage is open."
                : "Your correction is recorded with your name and the score it replaces, and everyone in the league can see it."}
          </p>
        )}
        <SubmitButton fullWidth pendingLabel="Saving…" disabled={!check.ok || !dateOk || unchanged || (retired && retirementWinner === null)}>
          {SAVE[mode]}
        </SubmitButton>
      </div>
    </form>
  );
}
