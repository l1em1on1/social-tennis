import { Badge } from "@/components/ui/badge";
import { buttonClasses } from "@/components/ui/button";
import { Sheet } from "@/components/ui/sheet";
import { switchActingPlayer } from "@/lib/actions";
import { cx } from "@/lib/cx";
import type { Viewer } from "@/lib/viewer";

/**
 * "Playing as" control in the shell header. A User holding one Player sees
 * just the name; a User holding several opens a sheet and picks one. Each
 * choice is a plain form posting to a server action, so it works before
 * hydration too.
 */
export function PlayerSwitcher({ viewer }: { viewer: Viewer }) {
  const { actingPlayer, players } = viewer;
  if (!actingPlayer) {
    return null;
  }

  // The accessible name is the visible text ("Playing as Alex Junior"), so
  // voice-control users can say what they see (WCAG 2.5.3, label in name).
  const label = (
    <span className="flex flex-col items-end leading-tight">
      <span className="text-xs font-normal text-fg-muted">Playing as </span>
      <span data-testid="acting-player">{actingPlayer.name}</span>
    </span>
  );

  if (players.length === 1) {
    return <div className="text-sm font-semibold">{label}</div>;
  }

  return (
    <Sheet
      title="Play as"
      trigger={
        <>
          {label}
          <span className="sr-only">, switch player</span>
        </>
      }
      triggerVariant="ghost"
      triggerClassName="px-2 text-sm"
    >
      <ul className="flex flex-col gap-2">
        {players.map((player) => {
          const current = player.id === actingPlayer.id;
          return (
            <li key={player.id}>
              <form action={switchActingPlayer}>
                <input type="hidden" name="playerId" value={player.id} />
                <button
                  type="submit"
                  aria-pressed={current}
                  className={cx(buttonClasses(current ? "primary" : "secondary", true), "justify-between")}
                >
                  {player.name}
                  {current && <Badge tone="accent">Current</Badge>}
                </button>
              </form>
            </li>
          );
        })}
      </ul>
    </Sheet>
  );
}
