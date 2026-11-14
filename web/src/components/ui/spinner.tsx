import { cx } from "@/lib/cx";

/**
 * "Working on it". Decorative: whatever shows it also says so in words or
 * `aria-busy`. Spins unless the person asked for reduced motion, where it
 * pulses instead.
 */
export function Spinner({ className }: { className?: string }) {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      className={cx("size-5 shrink-0 animate-spin fill-none motion-reduce:animate-pulse", className)}
    >
      <circle cx="12" cy="12" r="9" className="stroke-current opacity-25" strokeWidth="3" />
      <path d="M21 12a9 9 0 0 0-9-9" className="stroke-current" strokeWidth="3" strokeLinecap="round" />
    </svg>
  );
}
