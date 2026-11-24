import { cx } from "@/lib/cx";

/**
 * The app's mark (#226): a tennis ball in the brand colour, its seams cut in
 * the surface behind it. Decorative: the app's name always sits beside it.
 */
export function BallMark({ className }: { className?: string }) {
  return (
    <svg aria-hidden="true" viewBox="0 0 24 24" className={cx("size-6 shrink-0", className)}>
      <circle cx="12" cy="12" r="11" className="fill-accent" />
      <path
        d="M4.2 4.6c3.4 3.6 3.4 11.2 0 14.8M19.8 4.6c-3.4 3.6-3.4 11.2 0 14.8"
        className="fill-none stroke-surface [stroke-linecap:round]"
        strokeWidth="1.7"
      />
    </svg>
  );
}
