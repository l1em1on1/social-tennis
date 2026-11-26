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
 * Shared look for an icon button: 44px, the shell's tap-target minimum, like
 * every Button (#224). With a word under its icon (#255) it is as high, and
 * only as wide as the word needs.
 */
export function iconButtonClasses(tone: IconButtonTone = "neutral", word = false): string {
  return cx(
    "inline-flex shrink-0 items-center justify-center transition-colors",
    word ? "h-11 min-w-14 flex-col gap-1 rounded-md px-1" : "size-11 rounded-full",
    "disabled:cursor-not-allowed disabled:opacity-50",
    tones[tone],
  );
}

/** What an icon button shows: its icon, and under it a word when it has one. */
export function IconButtonContent({ icon, word, pending }: { icon: IconName; word?: string; pending?: boolean }) {
  return (
    <>
      {pending ? <Spinner /> : <Icon name={icon} />}
      {word && <span className="text-[0.6875rem] leading-none font-semibold whitespace-nowrap">{word}</span>}
    </>
  );
}

type IconButtonProps = Omit<ComponentProps<"button">, "children" | "aria-label"> & {
  icon: IconName;
  /** Its accessible name, which is all a screen reader hears: say what and whom, "Remove Anna Smith". */
  label: string;
  /**
   * Shown small under the icon, for one that isn't plain alone (#255): a phone
   * has no tooltip. `label` starts with it, so it can be said as seen (WCAG 2.5.3).
   */
  word?: string;
  tone?: IconButtonTone;
};

/** A button that is an icon, or an icon and a word, named by its `label` (#224, #255). */
export function IconButton({ icon, label, word, tone, className, type = "button", ...props }: IconButtonProps) {
  return (
    <button type={type} aria-label={label} title={label} className={cx(iconButtonClasses(tone, Boolean(word)), className)} {...props}>
      <IconButtonContent icon={icon} word={word} />
    </button>
  );
}

/**
 * The icon submit button of a form with a server action: like `SubmitButton`,
 * a spinner and disabled while the action runs, so a second press sends nothing.
 */
export function SubmitIconButton({ icon, label, word, tone, className, disabled, ...props }: Omit<IconButtonProps, "type">) {
  const { pending } = useFormStatus();
  return (
    <button
      {...props}
      type="submit"
      aria-label={label}
      title={label}
      disabled={disabled || pending}
      aria-busy={pending || undefined}
      className={cx(iconButtonClasses(tone, Boolean(word)), className)}
    >
      <IconButtonContent icon={icon} word={word} pending={pending} />
    </button>
  );
}
