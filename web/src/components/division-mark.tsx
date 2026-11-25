/**
 * A Division beside names, in a row's space (#255): its number in a ring. The
 * word is there for a screen reader, and for anyone reading the page's text.
 */
export function DivisionMark({ division }: { division: number | string }) {
  return (
    <span
      title={`Division ${division}`}
      className="relative inline-flex h-6 min-w-6 shrink-0 items-center justify-center rounded-full border-2 border-accent px-1 align-middle text-xs leading-none font-bold text-accent tabular-nums"
    >
      <span className="sr-only">Division </span>
      {division}
    </span>
  );
}
