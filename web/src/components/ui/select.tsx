import type { ComponentProps } from "react";
import { cx } from "@/lib/cx";
import { controlClasses } from "./field";

/**
 * Native `<select>`: on a phone it opens the platform picker, which beats any
 * custom listbox for reach and accessibility. Use inside a Field.
 */
export function Select({ className, children, ...props }: ComponentProps<"select">) {
  return (
    <select className={cx(controlClasses, "appearance-auto", className)} {...props}>
      {children}
    </select>
  );
}
