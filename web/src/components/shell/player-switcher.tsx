import { Badge } from "@/components/ui/badge";
import { Sheet } from "@/components/ui/sheet";
import { SubmitButton } from "@/components/ui/submit-button";
import { switchActingPlayer } from "@/lib/actions";
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
  // From md the header also holds the nav (#225): a long name is cut short there, in full on hover.
  const label = (
    <span className="flex min-w-0 flex-col items-end leading-tight">
      <span className="text-xs font-normal text-fg-muted">Playing as </span>
      <span data-testid="acting-player" title={actingPlayer.name} className="md:block md:max-w-40 md:truncate">
        {actingPlayer.name}
      </span>
    </span>
  );

  if (players.length === 1) {
    return <div className="min-w-0 text-sm font-semibold on-court">{label}</div>;
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
      // On the court: the sheet it opens keeps the page's colours (#254).
      triggerClassName="min-w-0 px-2 text-sm on-court"
    >
      <ul className="flex flex-col gap-4">
        {players.map((player) => {
          const current = player.id === actingPlayer.id;
          return (
            <li key={player.id}>
              <form action={switchActingPlayer}>
                <input type="hidden" name="playerId" value={player.id} />
                <SubmitButton
                  aria-pressed={current}
                  variant={current ? "primary" : "secondary"}
                  fullWidth
                  className="justify-between"
                >
                  {player.name}
                  {current && <Badge tone="accent">Current</Badge>}
                </SubmitButton>
              </form>
            </li>
          );
        })}
      </ul>
    </Sheet>
  );
}
