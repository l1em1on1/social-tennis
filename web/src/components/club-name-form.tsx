import { Field, Input } from "@/components/ui/field";
import { SubmitButton } from "@/components/ui/submit-button";
import { createClub, renameClub } from "@/lib/actions";

/** Matches the API's limit (Club.NameMaxLength). */
const NAME_MAX_LENGTH = 200;

interface ClubNameFormProps {
  /** Unique on the page: there is one form per Club on Admin. */
  id: string;
  submitLabel: string;
  /** Renames this Club; without it the form creates one. */
  club?: { id: string; name: string };
}

/** The name of a Club: creates one, or renames the one given. */
export function ClubNameForm({ id, submitLabel, club }: ClubNameFormProps) {
  return (
    <form action={club ? renameClub : createClub} className="flex flex-col gap-4">
      {club && <input type="hidden" name="clubId" value={club.id} />}
      <Field id={id} label="Club name">
        {(control) => (
          <Input
            {...control}
            name="name"
            required
            maxLength={NAME_MAX_LENGTH}
            autoComplete="off"
            defaultValue={club?.name}
          />
        )}
      </Field>
      <SubmitButton fullWidth pendingLabel="Saving…">
        {submitLabel}
      </SubmitButton>
    </form>
  );
}
