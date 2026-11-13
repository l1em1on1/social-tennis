import Link from "next/link";
import type { ComponentProps } from "react";
import { cx } from "@/lib/cx";

export type ButtonVariant = "primary" | "secondary" | "ghost" | "danger";

const variants: Record<ButtonVariant, string> = {
  primary: "bg-accent text-accent-fg hover:bg-accent-hover",
  secondary: "border border-border bg-surface text-fg hover:bg-surface-muted",
  ghost: "text-fg hover:bg-surface-muted",
  danger: "bg-danger text-surface hover:bg-danger-hover",
};

/**
 * Shared look for anything button-shaped. `min-h-11` keeps every tap target at
 * least 44px tall — the mobile minimum the shell's acceptance criteria set.
 */
export function buttonClasses(variant: ButtonVariant = "primary", fullWidth = false): string {
  return cx(
    "inline-flex min-h-11 items-center justify-center gap-2 rounded-xl px-4 text-base font-semibold",
    "transition-colors disabled:cursor-not-allowed disabled:opacity-50",
    variants[variant],
    fullWidth && "w-full",
  );
}

type ButtonProps = ComponentProps<"button"> & { variant?: ButtonVariant; fullWidth?: boolean };

export function Button({ variant, fullWidth, className, type = "button", ...props }: ButtonProps) {
  return <button type={type} className={cx(buttonClasses(variant, fullWidth), className)} {...props} />;
}

type ButtonLinkProps = ComponentProps<typeof Link> & { variant?: ButtonVariant; fullWidth?: boolean };

/** A link that looks like a Button — navigation stays a real `<a>`. */
export function ButtonLink({ variant, fullWidth, className, ...props }: ButtonLinkProps) {
  return <Link className={cx(buttonClasses(variant, fullWidth), className)} {...props} />;
}
