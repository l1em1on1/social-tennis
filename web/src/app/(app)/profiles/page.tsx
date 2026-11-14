import type { Metadata } from "next";
import { PlayerNameForm } from "@/components/player-name-form";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Sheet } from "@/components/ui/sheet";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { SubmitButton } from "@/components/ui/submit-button";
import { logout, switchActingPlayer } from "@/lib/actions";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "Profiles" };

/** The Players on this login: add one, rename one, choose who to play as. And sign-out. */
export default async function ProfilesPage({ searchParams }: PageProps<"/profiles">) {
  const [viewer, { error }] = await Promise.all([getViewer(), searchParams]);

  return (
    <>
      <h1 className="text-2xl font-bold">Profiles</h1>

      {error === "name" && (
        <ErrorState title="That name didn't work" description="Enter a name of up to 100 characters." />
      )}

      <Card title="Players on this login">
        {viewer.players.length === 0 ? (
          <EmptyState
            title="No player yet"
            description="Add yourself to join clubs and leagues. A login that only runs leagues doesn't need one."
          />
        ) : (
          <ul className="-my-2 divide-y divide-border">
            {viewer.players.map((player) => {
              const acting = player.id === viewer.actingPlayer?.id;
              return (
                // Keyed by the name too: a rename remounts the row, so its sheet starts closed with the new name.
                <li key={`${player.id}:${player.name}`} className="flex flex-col gap-2 py-3">
                  <div className="flex min-h-11 items-center justify-between gap-3">
                    <span className="font-medium">{player.name}</span>
                    {acting && <Badge tone="accent">Playing as</Badge>}
                  </div>
                  <p className="text-sm text-fg-muted">
                    {player.clubs.length === 0
                      ? "Not in a club yet"
                      : `Member of ${player.clubs.map((club) => club.name).join(", ")}`}
                  </p>
                  <div className="flex flex-wrap gap-2">
                    {!acting && (
                      <form action={switchActingPlayer}>
                        <input type="hidden" name="playerId" value={player.id} />
                        <SubmitButton variant="secondary">Play as {player.name.split(" ")[0]}</SubmitButton>
                      </form>
                    )}
                    <Sheet
                      title={`Rename ${player.name}`}
                      trigger={
                        <>
                          Rename<span className="sr-only"> {player.name}</span>
                        </>
                      }
                    >
                      <PlayerNameForm
                        id={`name-${player.id}`}
                        label="Name"
                        submitLabel="Save name"
                        from="/profiles"
                        player={player}
                      />
                    </Sheet>
                  </div>
                </li>
              );
            })}
          </ul>
        )}

        {/* Keyed by the count: adding a Player remounts the sheet, so it starts empty next time. */}
        <div className="mt-4" key={viewer.players.length}>
          <Sheet title="Add a player" trigger={viewer.players.length === 0 ? "Add yourself" : "Add another player"}>
            <PlayerNameForm
              id="new-player-name"
              label="Name"
              hint={
                viewer.players.length === 0
                  ? "The name other players see on ladders and scores."
                  : "For someone you manage on this login, such as a child."
              }
              submitLabel="Add player"
              from="/profiles"
            />
          </Sheet>
        </div>
      </Card>

      <Card title="Account">
        <p className="mb-3 text-sm text-fg-muted">Signed in as {viewer.email}</p>
        <form action={logout}>
          <SubmitButton variant="secondary" fullWidth pendingLabel="Logging out…">
            Log out
          </SubmitButton>
        </form>
      </Card>
    </>
  );
}
