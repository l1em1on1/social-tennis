import type { ComponentProps, ReactNode } from "react";
import { cx } from "@/lib/cx";

interface CardProps extends Omit<ComponentProps<"section">, "title"> {
  title?: ReactNode;
  /** Right-aligned header slot, e.g. a Badge or a link. */
  aside?: ReactNode;
}

export function Card({ title, aside, className, children, ...props }: CardProps) {
  return (
    <section className={cx("rounded-3xl border border-border bg-surface p-4 shadow-card", className)} {...props}>
      {(title || aside) && (
        <header className="mb-3 flex items-center justify-between gap-3">
          {/* A serif title with a small terracotta mark (#226). */}
          {title && (
            <h2 className="flex items-center gap-2 font-display text-lg font-semibold before:h-[1.1em] before:w-1 before:shrink-0 before:rounded-full before:bg-accent before:content-['']">
              {title}
            </h2>
          )}
          {aside}
        </header>
      )}
      {children}
    </section>
  );
}
