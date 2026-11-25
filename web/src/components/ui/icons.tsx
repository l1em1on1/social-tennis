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
  up: "M6 15l6-6 6 6",
  down: "M6 9l6 6 6-6",
  next: "M9 5l7 7-7 7",
  pencil: "M4 20l1-4L16 5l3 3L8 19zM14 7l3 3",
  leave: "M14 4H5v16h9M10 12h11M17 8l4 4-4 4",
  personMinus: "M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM2 21a7 7 0 0 1 14 0M16 11h6",
  personPlus: "M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM2 21a7 7 0 0 1 14 0M16 11h6M19 8v6",
  unlink: "M10 14l-2 2a3.5 3.5 0 0 1-5-5l3-3M14 10l2-2a3.5 3.5 0 0 1 5 5l-3 3M9 3v3M3 8h3M15 21v-3M21 16h-3",
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
