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
 */
export function SearchForm({ action, id, label, hint, defaultValue, maxLength }: SearchFormProps) {
  return (
    <Form action={action} role="search" className="flex items-end gap-2">
      <div className="min-w-0 flex-1">
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
      </div>
      <Button type="submit" variant="secondary">
        Search
      </Button>
    </Form>
  );
}
