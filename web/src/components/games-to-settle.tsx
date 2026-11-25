import Link from "next/link";
import { DivisionMark } from "./division-mark";
import { Icon } from "./ui/icons";
import { LinkPending } from "./ui/link-pending";

/**
 * Games with no score, for whoever runs the League to settle (#10): each row
 * opens its Match screen (#255), where it is recorded or marked not played.
 */
export function GamesToSettle({ games }: { games: { id: string; division: number; label: string }[] }) {
  return (
    <ul aria-label="Games with no score" className="divide-y divide-border text-sm">
      {games.map((game) => (
        <li key={game.id}>
          <Link href={`/games/${game.id}`} className="flex min-h-11 items-center gap-2 py-2 hover:underline">
            <DivisionMark division={game.division} />
            {/* The space is for the link's name: a flex row has none of its own. */}
            <span className="min-w-0 flex-1 break-words"> {game.label} </span>
            <span className="inline-flex shrink-0 items-center gap-0.5 text-xs font-semibold text-accent">
              Settle
              <LinkPending className="size-5">
                <Icon name="next" />
              </LinkPending>
            </span>
          </Link>
        </li>
      ))}
    </ul>
  );
}
