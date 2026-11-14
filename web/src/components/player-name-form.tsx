import { Field, Input } from "@/components/ui/field";
import { SubmitButton } from "@/components/ui/submit-button";
import { createPlayer, renamePlayer } from "@/lib/actions";

/** Matches the API's limit (Player.NameMaxLength). */
const NAME_MAX_LENGTH = 100;

interface PlayerNameFormProps {
  /** Unique on the page: there is one form per Player on Profiles. */
  id: string;
  label: string;
  hint?: string;
  submitLabel: string;
  /** The page the form is on, where a refused name is reported. */
  from: "/" | "/profiles";
  /** Renames this Player; without it the form adds a new one. */
  player?: { id: string; name: string };
}

/** The Name of a Player: adds one, or renames the one given. */
export function PlayerNameForm({ id, label, hint, submitLabel, from, player }: PlayerNameFormProps) {
  return (
    <form action={player ? renamePlayer : createPlayer} className="flex flex-col gap-4">
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
      <SubmitButton fullWidth pendingLabel="Saving…">
        {submitLabel}
      </SubmitButton>
    </form>
  );
}
