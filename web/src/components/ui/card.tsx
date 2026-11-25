import type { ComponentProps, ReactNode } from "react";
import { cx } from "@/lib/cx";

interface CardProps extends Omit<ComponentProps<"section">, "title"> {
  title?: ReactNode;
  /** Right-aligned header slot, e.g. a Badge or a link. */
  aside?: ReactNode;
}

export function Card({ title, aside, className, children, ...props }: CardProps) {
  return (
    <section className={cx("overflow-hidden rounded-md bg-surface p-4 shadow-card", className)} {...props}>
      {(title || aside) && (
        // The title on the court's blue, ruled with a white line like a court's (#254, Hard court).
        <header className="-mx-4 -mt-4 mb-5 flex items-center justify-between gap-3 bg-court px-4 py-2 text-court-fg shadow-[inset_0_-3px_0_#fff,0_3px_0_var(--st-court)]">
          {title && <h2 className="min-w-0 font-display text-2xl leading-tight font-extrabold break-words uppercase">{title}</h2>}
          {aside}
        </header>
      )}
      {children}
    </section>
  );
}
