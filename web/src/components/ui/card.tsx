import type { ComponentProps, ReactNode } from "react";
import { cx } from "@/lib/cx";

interface CardProps extends Omit<ComponentProps<"section">, "title"> {
  title?: ReactNode;
  /** Right-aligned header slot, e.g. a Badge or a link. */
  aside?: ReactNode;
}

export function Card({ title, aside, className, children, ...props }: CardProps) {
  return (
    <section className={cx("rounded-2xl border border-border bg-surface p-4", className)} {...props}>
      {(title || aside) && (
        <header className="mb-3 flex items-center justify-between gap-3">
          {title && <h2 className="text-base font-semibold">{title}</h2>}
          {aside}
        </header>
      )}
      {children}
    </section>
  );
}
