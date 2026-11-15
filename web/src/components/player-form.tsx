import { Field, Input } from "@/components/ui/field";
import { Select } from "@/components/ui/select";
import { SubmitButton } from "@/components/ui/submit-button";
import { createPlayer, updatePlayer } from "@/lib/actions";
import type { PlayerGender } from "@/lib/viewer";

/** Matches the API's limit (Player.NameMaxLength). */
const NAME_MAX_LENGTH = 100;

/** The club's informal grouping (Player.MinLevel to MaxLevel). */
const LEVELS = [1, 2, 3, 4, 5];

interface PlayerFormProps {
  /** Unique on the page: there is one form per Player on Profiles. */
  id: string;
  label: string;
  hint?: string;
  submitLabel: string;
  /** The page the form is on, where a refused value is reported. */
  from: "/" | "/profiles";
  /**
   * Also ask for Gender and Level. The first sign-in doesn't: one question,
   * and the rest can wait for Profiles.
   */
  details?: boolean;
  /** Edits this Player; without it the form adds a new one. */
  player?: { id: string; name: string; gender: PlayerGender | null; level: number | null };
}

/** A Player's own fields: adds one, or edits the one given. Rating isn't here: a Manager sets it. */
export function PlayerForm({ id, label, hint, submitLabel, from, details = false, player }: PlayerFormProps) {
  return (
    <form action={player ? updatePlayer : createPlayer} className="flex flex-col gap-4">
      <input type="hidden" name="from" value={from} />
      {player && <input type="hidden" name="playerId" value={player.id} />}
      <Field id={id} label={label} hint={hint}>
        {(control) => (
          <Input
            {...control}
            name="name"
            required
            maxLength={NAME_MAX_LENGTH}
            autoComplete={player ? "off" : "name"}
            defaultValue={player?.name}
          />
        )}
      </Field>
      {details && (
        <>
          <Field id={`${id}-gender`} label="Plays as" hint="For men's, women's and mixed leagues.">
            {(control) => (
              <Select {...control} name="gender" defaultValue={player?.gender ?? ""}>
                <option value="">Not set</option>
                <option value="Male">Male</option>
                <option value="Female">Female</option>
              </Select>
            )}
          </Field>
          <Field id={`${id}-level`} label="Level" hint="The club's grouping, 1 to 5. Leave it unset if you don't know.">
            {(control) => (
              <Select {...control} name="level" defaultValue={player?.level ?? ""}>
                <option value="">Not set</option>
                {LEVELS.map((level) => (
                  <option key={level} value={level}>
                    {level}
                  </option>
                ))}
              </Select>
            )}
          </Field>
        </>
      )}
      <SubmitButton fullWidth pendingLabel="Saving…">
        {submitLabel}
      </SubmitButton>
    </form>
  );
}
