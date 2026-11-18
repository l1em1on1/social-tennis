import { ButtonLink } from "@/components/ui/button";

interface PagerProps {
  /** What is paged, for the navigation's accessible name: "users" gives "Pages of users". */
  label: string;
  /** The previous page, or nothing on the first. */
  previousHref?: string;
  /** The next page, or nothing on the last. */
  nextHref?: string;
}

/**
 * Previous and Next for a paged list (#134). Links, not buttons: each page is
 * a URL. Previous stays on the left and Next on the right, so they don't swap
 * sides between pages. Renders nothing when the whole list fits on one page.
 */
export function Pager({ label, previousHref, nextHref }: PagerProps) {
  if (!previousHref && !nextHref) {
    return null;
  }
  return (
    <nav aria-label={`Pages of ${label}`} className="flex justify-between gap-2">
      {previousHref ? (
        <ButtonLink href={previousHref} variant="secondary">
          Previous
        </ButtonLink>
      ) : (
        <span />
      )}
      {nextHref && (
        <ButtonLink href={nextHref} variant="secondary">
          Next
        </ButtonLink>
      )}
    </nav>
  );
}

/** "51–100 of 312": the window a page shows, 1-based. Empty when the page is. */
export function pageRange(offset: number, count: number, total: number) {
  return count > 0 ? `${offset + 1}–${offset + count} of ${total}` : "";
}
