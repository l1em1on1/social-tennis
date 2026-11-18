import Form from "next/form";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";

interface SearchFormProps {
  /** The page the search opens, with the text as its `q` parameter. */
  action: string;
  /** Unique on the page: ties the label and hint to the box. */
  id: string;
  label: string;
  hint?: string;
  /** The search now in force, shown in the box. */
  defaultValue?: string;
  maxLength?: number;
}

/**
 * A search box and its button. A GET form through `next/form`, so a search is
 * a URL (`?q=`): it survives a reload, and paging links can keep it. Client-side
 * navigation, with a plain form submit as the fallback (Next.js docs, Form).
 *
 * The button goes under the box, both full width, as in the app's other forms.
 * Beside it, on a phone, the button left an email address too little room.
 */
export function SearchForm({ action, id, label, hint, defaultValue, maxLength }: SearchFormProps) {
  return (
    <Form action={action} role="search" className="flex flex-col gap-4">
      <Field id={id} label={label} hint={hint}>
        {(control) => (
          <Input
            {...control}
            type="search"
            name="q"
            defaultValue={defaultValue}
            maxLength={maxLength}
            autoComplete="off"
          />
        )}
      </Field>
      <Button type="submit" variant="secondary" fullWidth>
        Search
      </Button>
    </Form>
  );
}
