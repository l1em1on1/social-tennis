"use client";

import { useState, useSyncExternalStore } from "react";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";

const noSubscription = () => () => {};

/**
 * A value to copy, such as a Club's join link (#222): shown read-only, so a
 * long press selects it on any phone, and a Copy button where the browser
 * allows the clipboard (a secure page only). If the browser refuses the copy
 * anyway, the button selects the value instead, for the person to copy.
 */
export function CopyField({ id, label, hint, value }: { id: string; label: string; hint?: string; value: string }) {
  const canCopy = useSyncExternalStore(
    noSubscription,
    () => window.isSecureContext && navigator.clipboard !== undefined,
    () => false,
  );
  const [copied, setCopied] = useState<boolean | null>(null);

  return (
    <div className="flex flex-col gap-2">
      <Field id={id} label={label} hint={hint}>
        {(control) => <Input {...control} readOnly value={value} onFocus={(event) => event.currentTarget.select()} />}
      </Field>
      {canCopy && (
        <Button
          variant="secondary"
          onClick={async () => {
            try {
              await navigator.clipboard.writeText(value);
              setCopied(true);
            } catch {
              setCopied(false);
              (document.getElementById(id) as HTMLInputElement | null)?.select();
            }
          }}
        >
          {copied ? "Copied" : copied === false ? "Selected: copy it from the box" : "Copy link"}
        </Button>
      )}
    </div>
  );
}
