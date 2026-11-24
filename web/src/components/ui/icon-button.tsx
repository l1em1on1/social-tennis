"use client";

import type { ComponentProps } from "react";
import { useFormStatus } from "react-dom";
import { cx } from "@/lib/cx";
import { Icon, type IconName } from "./icons";
import { Spinner } from "./spinner";

/** An icon button's colour: quiet by default, red for removing, green for approving. */
export type IconButtonTone = "neutral" | "danger" | "accent";

const tones: Record<IconButtonTone, string> = {
  neutral: "text-fg-muted hover:bg-surface-muted hover:text-fg",
  danger: "text-danger hover:bg-danger-muted",
  accent: "text-accent hover:bg-accent-muted",
};

/**
 * Shared look for a square icon button: 44px, the shell's tap-target minimum,
 * like every Button (#224).
 */
export function iconButtonClasses(tone: IconButtonTone = "neutral"): string {
  return cx(
    "inline-flex size-11 shrink-0 items-center justify-center rounded-full transition-colors",
    "disabled:cursor-not-allowed disabled:opacity-50",
    tones[tone],
  );
}

type IconButtonProps = Omit<ComponentProps<"button">, "children" | "aria-label"> & {
  icon: IconName;
  /** Its accessible name, which is all a screen reader hears: say what and whom, "Remove Anna Smith". */
  label: string;
  tone?: IconButtonTone;
};

/** A button that is only an icon, named by its `label` (#224). */
export function IconButton({ icon, label, tone, className, type = "button", ...props }: IconButtonProps) {
  return (
    <button type={type} aria-label={label} title={label} className={cx(iconButtonClasses(tone), className)} {...props}>
      <Icon name={icon} />
    </button>
  );
}

/**
 * The icon submit button of a form with a server action: like `SubmitButton`,
 * a spinner and disabled while the action runs, so a second press sends nothing.
 */
export function SubmitIconButton({ icon, label, tone, className, disabled, ...props }: Omit<IconButtonProps, "type">) {
  const { pending } = useFormStatus();
  return (
    <button
      {...props}
      type="submit"
      aria-label={label}
      title={label}
      disabled={disabled || pending}
      aria-busy={pending || undefined}
      className={cx(iconButtonClasses(tone), className)}
    >
      {pending ? <Spinner /> : <Icon name={icon} />}
    </button>
  );
}
