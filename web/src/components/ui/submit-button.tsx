"use client";

import type { ComponentProps, ReactNode } from "react";
import { useFormStatus } from "react-dom";
import { cx } from "@/lib/cx";
import { buttonClasses, type ButtonVariant } from "./button";
import { Spinner } from "./spinner";

type SubmitButtonProps = Omit<ComponentProps<"button">, "type"> & {
  variant?: ButtonVariant;
  fullWidth?: boolean;
  /** Shown in place of the label while the form's action runs, e.g. "Sending…". */
  pendingLabel?: ReactNode;
};

/**
 * The submit button of a form with a server action. While the action runs it
 * shows a spinner and is disabled, so an impatient second press sends nothing
 * (`useFormStatus`, per the Next.js forms guide). Every form that changes
 * something uses this, not a bare Button.
 *
 * Before hydration it is an ordinary submit button, and the form still works.
 */
export function SubmitButton({
  variant,
  fullWidth,
  pendingLabel,
  className,
  children,
  disabled,
  ...props
}: SubmitButtonProps) {
  const { pending } = useFormStatus();

  return (
    <button
      {...props}
      type="submit"
      disabled={disabled || pending}
      aria-busy={pending || undefined}
      className={cx(buttonClasses(variant, fullWidth), className)}
    >
      {pending && <Spinner />}
      {pending && pendingLabel ? pendingLabel : children}
    </button>
  );
}
