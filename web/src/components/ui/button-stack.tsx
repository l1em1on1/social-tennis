import type { ReactNode } from "react";

/**
 * Buttons one below another, 16px apart (#255): close enough to read as a set,
 * far enough to hit the right one with a thumb.
 */
export function ButtonStack({ children }: { children: ReactNode }) {
  return <div className="flex flex-col gap-4">{children}</div>;
}

/** Buttons side by side, 8px apart, and 16px when they wrap onto a second line. */
export function ButtonRow({ children }: { children: ReactNode }) {
  return <div className="flex flex-wrap gap-x-2 gap-y-4">{children}</div>;
}
