import type { ReactNode } from "react";
import { cx } from "@/lib/cx";

interface StateProps {
  title: string;
  description?: ReactNode;
  action?: ReactNode;
}

/** "Nothing here yet", said usefully: what's missing, and what happens next. */
export function EmptyState({ title, description, action }: StateProps) {
  return (
    <div className="flex flex-col items-center gap-2 rounded-2xl border border-dashed border-border px-4 py-8 text-center">
      <p className="font-semibold">{title}</p>
      {description && <p className="max-w-xs text-sm text-fg-muted">{description}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}

/** Something failed. `role="alert"` so a screen reader announces it on arrival. */
export function ErrorState({ title, description, action }: StateProps) {
  return (
    <div role="alert" className="flex flex-col gap-2 rounded-2xl border border-danger bg-danger-muted px-4 py-4">
      <p className="font-semibold text-danger">{title}</p>
      {description && <p className="text-sm text-fg">{description}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}

/** Placeholder block shaped like the content that's loading. Hidden from assistive tech. */
export function Skeleton({ className }: { className?: string }) {
  return <div aria-hidden="true" className={cx("animate-pulse rounded-xl bg-surface-muted", className)} />;
}

/**
 * The loading pattern: a labelled status region wrapping Skeleton blocks, so
 * screen readers hear "Loading" once instead of nothing (the blocks are hidden).
 */
export function SkeletonGroup({ label = "Loading", children }: { label?: string; children: ReactNode }) {
  return (
    <div role="status" aria-label={label} className="flex flex-col gap-3">
      {children}
    </div>
  );
}
