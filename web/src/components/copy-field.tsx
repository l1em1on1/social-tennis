"use client";

import { useState, useSyncExternalStore } from "react";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";

const noSubscription = () => () => {};

/**
 * A value to copy, such as a Club's join link (#222): shown read-only, so a
 * long press selects it on any phone, and a Copy button where the browser
 * allows the clipboard (a secure page only).
 */
export function CopyField({ id, label, hint, value }: { id: string; label: string; hint?: string; value: string }) {
  const canCopy = useSyncExternalStore(
    noSubscription,
    () => window.isSecureContext && navigator.clipboard !== undefined,
    () => false,
  );
  const [copied, setCopied] = useState(false);

  return (
    <div className="flex flex-col gap-2">
      <Field id={id} label={label} hint={hint}>
        {(control) => <Input {...control} readOnly value={value} onFocus={(event) => event.currentTarget.select()} />}
      </Field>
      {canCopy && (
        <Button
          variant="secondary"
          onClick={async () => {
            await navigator.clipboard.writeText(value);
            setCopied(true);
          }}
        >
          {copied ? "Copied" : "Copy link"}
        </Button>
      )}
    </div>
  );
}
