import Link from "next/link";
import type { ComponentProps } from "react";
import { cx } from "@/lib/cx";
import { LinkPending } from "./link-pending";

export type ButtonVariant = "primary" | "secondary" | "ghost" | "danger";

const variants: Record<ButtonVariant, string> = {
  // The ball's yellow, with a shadow under its edge (#254, Hard court).
  primary: "bg-ball text-ball-fg shadow-[inset_0_-3px_0_rgb(11_31_58/0.28)] hover:bg-ball-hover",
  secondary: "border-2 border-accent bg-surface text-accent hover:bg-accent-muted",
  ghost: "text-fg hover:bg-surface-muted",
  danger: "bg-danger text-surface hover:bg-danger-hover",
};

/**
 * Shared look for anything button-shaped. `min-h-11` keeps every tap target at
 * least 44px tall — the mobile minimum the shell's acceptance criteria set.
 */
export function buttonClasses(variant: ButtonVariant = "primary", fullWidth = false): string {
  return cx(
    // Squared off, as the court's lines are (#254, Hard court).
    "inline-flex min-h-11 items-center justify-center gap-2 rounded-md px-5 text-base font-semibold",
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

/** A link that looks like a Button — navigation stays a real `<a>`. Shows a spinner while the next page loads. */
export function ButtonLink({ variant, fullWidth, className, children, ...props }: ButtonLinkProps) {
  return (
    <Link className={cx(buttonClasses(variant, fullWidth), className)} {...props}>
      <LinkPending />
      {children}
    </Link>
  );
}
