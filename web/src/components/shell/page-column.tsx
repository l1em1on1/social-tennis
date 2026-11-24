import type { ReactNode } from "react";

/**
 * A page that reads best as one column, such as a form, the Match screen or
 * Profiles (#225): a phone's width below the `md` breakpoint, as every page
 * is, and centred at `max-w-2xl` above it, where the shell's content is
 * wider. Pages that use the width (Home, Manage, the ladder) lay out their
 * own columns instead.
 */
export function PageColumn({ children }: { children: ReactNode }) {
  return <div className="flex flex-col gap-4 md:mx-auto md:w-full md:max-w-2xl">{children}</div>;
}
