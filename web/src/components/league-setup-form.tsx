"use client";

import { useActionState, useState } from "react";
import { Field, Input } from "@/components/ui/field";
import { Select } from "@/components/ui/select";
import { ErrorState } from "@/components/ui/states";
import { SubmitButton } from "@/components/ui/submit-button";
import { createLeague, type CreateLeagueState } from "@/lib/actions";
import { cx } from "@/lib/cx";
import {
  DIVISION_SIZE,
  LEAGUE_TYPES,
  STANDINGS_MODES,
  describeScoreFormat,
  isValidCombination,
  type LeagueStructure,
  type LeagueType,
  type StageRoll,
  type StandingsMode,
} from "@/lib/league";

/** Matches the API's limit (League.NameMaxLength). */
const NAME_MAX_LENGTH = 200;

interface Choice<T extends string> {
  value: T;
  label: string;
  disabled?: boolean;
}

/**
 * A row of radio buttons that reads as a segmented control. Real radios in a
 * fieldset, so the group has a name and arrow keys move between choices.
 */
function ChoiceGroup<T extends string>({
  name,
  legend,
  choices,
  value,
  onChange,
  hint,
  error,
}: {
  name: string;
  legend: string;
  choices: Choice<T>[];
  value: T;
  onChange: (value: T) => void;
  hint?: string;
  error?: string;
}) {
  const hintId = hint ? `${name}-hint` : undefined;
  const errorId = error ? `${name}-error` : undefined;
  return (
    <fieldset className="flex flex-col gap-1.5" aria-describedby={[hintId, errorId].filter(Boolean).join(" ") || undefined}>
      <legend className="mb-1.5 text-sm font-medium">{legend}</legend>
      <div className="flex rounded-md border border-border-strong p-1">
        {choices.map((choice) => (
          <label
            key={choice.value}
            className={cx(
              "flex min-h-10 flex-1 cursor-pointer items-center justify-center rounded-lg px-2 text-center text-sm font-semibold",
              "has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-accent",
              value === choice.value ? "bg-accent text-accent-fg" : "text-fg",
              choice.disabled && "cursor-not-allowed opacity-50",
            )}
          >
            <input
              type="radio"
              name={name}
              value={choice.value}
              checked={value === choice.value}
              disabled={choice.disabled}
              onChange={() => onChange(choice.value)}
              className="sr-only"
            />
            {choice.label}
          </label>
        ))}
      </div>
      {hint && (
        <p id={hintId} className="text-sm text-fg-muted">
          {hint}
        </p>
      )}
      {error && (
        <p id={errorId} className="text-sm font-medium text-danger">
          {error}
        </p>
      )}
    </fieldset>
  );
}

const initialState: CreateLeagueState = { errors: {} };

/**
 * Create a League (wireframe M2). The rules the API enforces are mirrored so
 * a Manager can't build a League it would refuse: Foursomes and Knockout can't
 * both be picked, the Match Tiebreak switch exists only for best of three, and
 * the roll mode only for Divisional, and the Standings choice only where a
 * Division of Teams ranks (#214). The API still decides, and its messages
 * show next to the fields they name.
 */
export function LeagueSetupForm({ clubId, today }: { clubId: string; today: string }) {
  const [state, formAction] = useActionState(createLeague, initialState);
  const [type, setType] = useState<LeagueType>("SinglesMixed");
  const [structure, setStructure] = useState<LeagueStructure>("Divisional");
  const [gamesPerSet, setGamesPerSet] = useState<"6" | "9">("6");
  const [setsPerGame, setSetsPerGame] = useState<"1" | "3">("1");
  const [matchTiebreak, setMatchTiebreak] = useState(false);
  const [stageRoll, setStageRoll] = useState<StageRoll>("Manual");
  const [standingsMode, setStandingsMode] = useState<StandingsMode>("Standard");
  // Controlled, every one: React resets a form's uncontrolled fields when its
  // action finishes, which would wipe them on a refusal.
  const [name, setName] = useState("");
  const [startDate, setStartDate] = useState(today);
  const [endDate, setEndDate] = useState("");
  const [stageCount, setStageCount] = useState("1");
  const [stageUnit, setStageUnit] = useState<"Months" | "Weeks">("Months");
  const [divisionSize, setDivisionSize] = useState(String(DIVISION_SIZE.default));

  const error = (field: string) => state.errors[field]?.join(" ");
  const fieldErrors = Object.entries(state.errors).filter(([, messages]) => messages.length > 0);
  const preview = describeScoreFormat({
    gamesPerSet,
    setsPerGame,
    matchTiebreakDecider: setsPerGame === "3" && matchTiebreak,
  });

  return (
    <form action={formAction} className="flex flex-col gap-5">
      <input type="hidden" name="clubId" value={clubId} />

      {fieldErrors.length > 0 && (
        <ErrorState
          title="The League wasn't created"
          description={error("form") ?? "Fix the fields marked below and try again."}
        />
      )}

      <Field id="league-name" label="Name" hint="As Players will see it." error={error("name")}>
        {(control) => (
          <Input
            {...control}
            name="name"
            required
            maxLength={NAME_MAX_LENGTH}
            autoComplete="off"
            value={name}
            onChange={(event) => setName(event.target.value)}
          />
        )}
      </Field>

      <Field
        id="league-type"
        label="Type"
        hint={structure === "Knockout" ? "Foursomes is played in Divisions only, so it isn't offered for Knockout." : undefined}
        error={error("type")}
      >
        {(control) => (
          <Select {...control} name="type" value={type} onChange={(event) => setType(event.target.value as LeagueType)}>
            {LEAGUE_TYPES.map((option) => (
              <option key={option.value} value={option.value} disabled={!isValidCombination(option.value, structure)}>
                {option.label}
              </option>
            ))}
          </Select>
        )}
      </Field>

      <ChoiceGroup
        name="structure"
        legend="Structure"
        value={structure}
        onChange={setStructure}
        choices={[
          { value: "Divisional", label: "Divisional ladder" },
          { value: "Knockout", label: "Knockout", disabled: !isValidCombination(type, "Knockout") },
        ]}
        hint={
          type === "Foursomes"
            ? "Foursomes is a Division of 4 rotating partners, so it can't be Knockout."
            : structure === "Divisional"
              ? "Divisions move up and down at the end of every Stage."
              : "A Bracket; each round gets one Stage to play its Games."
        }
        error={error("structure")}
      />

      <div className="flex flex-col gap-4 sm:flex-row">
        <Field id="league-start" label="Starts" error={error("startDate")}>
          {(control) => (
            <Input
              {...control}
              name="startDate"
              type="date"
              required
              value={startDate}
              onChange={(event) => setStartDate(event.target.value)}
            />
          )}
        </Field>
        <Field id="league-end" label="Ends" error={error("endDate")}>
          {(control) => (
            <Input
              {...control}
              name="endDate"
              type="date"
              required
              min={startDate || undefined}
              value={endDate}
              onChange={(event) => setEndDate(event.target.value)}
            />
          )}
        </Field>
      </div>

      <fieldset className="flex flex-col gap-1.5">
        <legend className="mb-1.5 text-sm font-medium">
          {structure === "Divisional" ? "Stage length (how often Divisions move)" : "Stage length (time for each round)"}
        </legend>
        <div className="flex gap-2">
          <Input
            name="stageCount"
            type="number"
            inputMode="numeric"
            min={1}
            max={12}
            step={1}
            required
            value={stageCount}
            onChange={(event) => setStageCount(event.target.value)}
            aria-label="Stage length"
            aria-invalid={error("stageLength.count") ? true : undefined}
            className="w-24"
          />
          <Select
            name="stageUnit"
            value={stageUnit}
            onChange={(event) => setStageUnit(event.target.value as "Months" | "Weeks")}
            aria-label="Stage length unit"
          >
            <option value="Months">Calendar months</option>
            <option value="Weeks">Weeks</option>
          </Select>
        </div>
        {error("stageLength.count") && (
          <p className="text-sm font-medium text-danger">{error("stageLength.count")}</p>
        )}
      </fieldset>

      {structure === "Divisional" && type !== "Foursomes" && (
        <Field
          id="league-division-size"
          label="Teams per Division"
          hint="Everyone in a Division plays everyone else once a Stage. At the start you can make a Division a Team bigger or smaller."
          error={error("divisionSize")}
        >
          {(control) => (
            <Input
              {...control}
              name="divisionSize"
              type="number"
              inputMode="numeric"
              min={DIVISION_SIZE.min}
              max={DIVISION_SIZE.max}
              step={1}
              required
              value={divisionSize}
              onChange={(event) => setDivisionSize(event.target.value)}
              className="w-24"
            />
          )}
        </Field>
      )}

      {structure === "Divisional" && type !== "Foursomes" && (
        <ChoiceGroup
          name="standingsMode"
          legend="Standings"
          value={standingsMode}
          onChange={setStandingsMode}
          choices={STANDINGS_MODES.map(({ value, label }) => ({ value, label }))}
          hint={STANDINGS_MODES.find((mode) => mode.value === standingsMode)?.rule}
          error={error("standingsMode")}
        />
      )}

      {structure === "Divisional" && (
        <ChoiceGroup
          name="stageRoll"
          legend="When a Stage ends"
          value={stageRoll}
          onChange={setStageRoll}
          choices={[
            { value: "Manual", label: "I close it" },
            { value: "Automatic", label: "Closes automatically" },
          ]}
          hint={
            stageRoll === "Manual"
              ? "Scores stay editable until you close the Stage."
              : "It closes on its end date, once nothing is waiting for you."
          }
          error={error("stageRoll")}
        />
      )}

      <ChoiceGroup
        name="gamesPerSet"
        legend="Games per set"
        value={gamesPerSet}
        onChange={setGamesPerSet}
        choices={[
          { value: "6", label: "6" },
          { value: "9", label: "9" },
        ]}
        error={error("scoreFormat.gamesPerSet")}
      />

      <ChoiceGroup
        name="setsPerGame"
        legend="Sets"
        value={setsPerGame}
        onChange={setSetsPerGame}
        choices={[
          { value: "1", label: "One set" },
          { value: "3", label: "Best of 3" },
        ]}
        error={error("scoreFormat.setsPerGame")}
      />

      {setsPerGame === "3" && (
        <label className="flex items-start gap-3 rounded-md border border-border p-3">
          <input
            type="checkbox"
            name="matchTiebreakDecider"
            checked={matchTiebreak}
            onChange={(event) => setMatchTiebreak(event.target.checked)}
            className="mt-1 size-5 accent-[var(--color-accent)]"
          />
          <span>
            <span className="block font-medium">Match tiebreak decider</span>
            <span className="block text-sm text-fg-muted">
              At one set all, play first to 10 points instead of a third set.
            </span>
          </span>
        </label>
      )}
      {error("scoreFormat.matchTiebreakDecider") && (
        <p className="text-sm font-medium text-danger">{error("scoreFormat.matchTiebreakDecider")}</p>
      )}

      <p className="rounded-md border border-border bg-surface-muted px-4 py-3 text-sm">
        <strong>Format:</strong> {preview}.
      </p>

      <SubmitButton fullWidth pendingLabel="Creating…">
        Create League
      </SubmitButton>
      <p className="text-sm text-fg-muted">The type and format can&apos;t be changed after the League is created.</p>
    </form>
  );
}
