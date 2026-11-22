"use client";

import { cx } from "@/lib/cx";

interface StepperProps {
  /** Names the group, e.g. "Set 1, Una": its buttons are "One less" and "One more". */
  label: string;
  value: number;
  max: number;
  onChange: (value: number) => void;
  className?: string;
}

const step = cx(
  "inline-flex size-11 items-center justify-center rounded-xl border border-border-strong bg-surface text-xl font-semibold",
  "transition-colors hover:bg-surface-muted disabled:cursor-not-allowed disabled:opacity-40",
);

/**
 * A number set by tapping − and +, from 0 to `max`: no keyboard pops up and
 * nothing can be mistyped (wireframe P4, score entry). Each button is a 44px
 * tap target, and the value is announced as it changes.
 */
export function Stepper({ label, value, max, onChange, className }: StepperProps) {
  return (
    <div role="group" aria-label={label} className={cx("inline-flex items-center gap-2", className)}>
      <button type="button" className={step} aria-label="One less" disabled={value <= 0} onClick={() => onChange(value - 1)}>
        −
      </button>
      {/* Announced as it changes; a plain span, not <output>, so the form's own status stays the only one. */}
      <span aria-live="polite" className="w-8 text-center text-xl font-bold tabular-nums">
        {value}
      </span>
      <button type="button" className={step} aria-label="One more" disabled={value >= max} onClick={() => onChange(value + 1)}>
        +
      </button>
    </div>
  );
}
