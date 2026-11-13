import type { ComponentProps, ReactNode } from "react";
import { cx } from "@/lib/cx";

/** Props a Field hands to its control, so label, hint, and error are wired for screen readers. */
export interface FieldControlProps {
  id: string;
  "aria-describedby"?: string;
  "aria-invalid"?: true;
}

interface FieldProps {
  id: string;
  label: string;
  hint?: string;
  error?: string;
  children: (control: FieldControlProps) => ReactNode;
}

/**
 * Label + control + hint/error. The control is a render prop so the ids that
 * tie them together are computed once, here, instead of by every caller.
 */
export function Field({ id, label, hint, error, children }: FieldProps) {
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(" ") || undefined;

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-medium">
        {label}
      </label>
      {children({ id, "aria-describedby": describedBy, "aria-invalid": error ? true : undefined })}
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
    </div>
  );
}

export const controlClasses = cx(
  "min-h-11 w-full rounded-xl border border-border-strong bg-surface px-3 text-base text-fg",
  "placeholder:text-fg-muted aria-invalid:border-danger",
);

export function Input({ className, ...props }: ComponentProps<"input">) {
  return <input className={cx(controlClasses, className)} {...props} />;
}
