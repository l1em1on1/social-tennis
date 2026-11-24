import { cx } from "@/lib/cx";

/**
 * The app's few icons (#224): 24px strokes drawn like the bottom nav's, owned
 * rather than a library (ADR-0014). Always decorative: whatever shows one names
 * it in words or an `aria-label`.
 */
const paths = {
  trash: "M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3",
  check: "M5 12.5 10 17.5 19 7",
  cross: "M6 6l12 12M18 6 6 18",
  more: "M5 12h.01M12 12h.01M19 12h.01",
} as const;

export type IconName = keyof typeof paths;

export function Icon({ name, className }: { name: IconName; className?: string }) {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      className={cx(
        "size-5 shrink-0 fill-none stroke-current stroke-2 [stroke-linecap:round] [stroke-linejoin:round]",
        // Three dots: drawn as round caps, so a heavier stroke.
        name === "more" && "stroke-[3]",
        className,
      )}
    >
      <path d={paths[name]} />
    </svg>
  );
}
