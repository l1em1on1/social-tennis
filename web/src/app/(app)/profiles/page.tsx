import type { Metadata } from "next";
import { PlayerForm } from "@/components/player-form";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Sheet } from "@/components/ui/sheet";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { SubmitButton } from "@/components/ui/submit-button";
import { leaveClub, logout, switchActingPlayer } from "@/lib/actions";
import { getViewer } from "@/lib/viewer";
import { PageColumn } from "@/components/shell/page-column";

export const metadata: Metadata = { title: "Profiles" };

/** The Players on this login: add one, edit one, choose who to play as, leave a Club. And sign-out. */
export default async function ProfilesPage({ searchParams }: PageProps<"/profiles">) {
  const [viewer, { error }] = await Promise.all([getViewer(), searchParams]);

  return (
    <PageColumn>
      <h1 className="text-2xl font-bold">Profiles</h1>

      {error === "name" && (
        <ErrorState title="That didn't save" description="Enter a name of up to 100 characters, and choose a level and gender from the lists." />
      )}
      {error === "limit" && (
        <ErrorState title="That's as many Players as one login can add" description="Ask a Manager to add anyone else." />
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
                // Keyed by what the sheet edits: saving remounts the row, so its sheet starts closed with the new values.
                <li
                  key={`${player.id}:${player.name}:${player.gender}:${player.level}`}
                  className="flex flex-col gap-2 py-3"
                >
                  <div className="flex min-h-11 items-center justify-between gap-3">
                    <div>
                      <p className="font-medium">{player.name}</p>
                      <p className="text-sm text-fg-muted">
                        {[player.gender, player.level === null ? null : `Level ${player.level}`]
                          .filter(Boolean)
                          .join(" · ") || "Level and gender not set"}
                      </p>
                    </div>
                    {acting && <Badge tone="accent">Playing as</Badge>}
                  </div>
                  {player.clubs.length === 0 ? (
                    <p className="text-sm text-fg-muted">Not in a club yet</p>
                  ) : (
                    <ul aria-label={`${player.name}'s clubs`} className="flex flex-col gap-2">
                      {player.clubs.map((club) => (
                        <li key={club.id} className="flex min-h-11 items-center justify-between gap-3">
                          <span className="text-sm text-fg-muted">
                            {club.approved ? `Member of ${club.name}` : `Waiting for approval at ${club.name}`}
                          </span>
                          {club.approved ? (
                            // Behind a sheet: coming back means asking a Manager again.
                            <Sheet
                              title={`Leave ${club.name}?`}
                              trigger={
                                <>
                                  Leave<span className="sr-only"> {club.name}</span>
                                </>
                              }
                            >
                              <p className="text-fg-muted">
                                {player.name} will no longer be a member of {club.name}. Joining again may need a
                                manager&apos;s approval.
                              </p>
                              <form action={leaveClub}>
                                <input type="hidden" name="clubId" value={club.id} />
                                <input type="hidden" name="playerId" value={player.id} />
                                <SubmitButton variant="danger" fullWidth pendingLabel="Leaving…">
                                  Leave club
                                </SubmitButton>
                              </form>
                            </Sheet>
                          ) : (
                            <form action={leaveClub}>
                              <input type="hidden" name="clubId" value={club.id} />
                              <input type="hidden" name="playerId" value={player.id} />
                              <SubmitButton
                                variant="secondary"
                                pendingLabel="Cancelling…"
                                aria-label={`Cancel request to join ${club.name}`}
                              >
                                Cancel request
                              </SubmitButton>
                            </form>
                          )}
                        </li>
                      ))}
                    </ul>
                  )}
                  <div className="flex flex-wrap gap-2">
                    {!acting && (
                      <form action={switchActingPlayer}>
                        <input type="hidden" name="playerId" value={player.id} />
                        <SubmitButton variant="secondary">Play as {player.name.split(" ")[0]}</SubmitButton>
                      </form>
                    )}
                    <Sheet
                      title={`Edit ${player.name}`}
                      trigger={
                        <>
                          Edit<span className="sr-only"> {player.name}</span>
                        </>
                      }
                    >
                      <PlayerForm
                        id={`player-${player.id}`}
                        label="Name"
                        submitLabel="Save"
                        from="/profiles"
                        details
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
            <PlayerForm
              id="new-player"
              label="Name"
              hint={
                viewer.players.length === 0
                  ? "The name other players see on ladders and scores."
                  : "For someone you manage on this login, such as a child."
              }
              submitLabel="Add player"
              from="/profiles"
              details
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
    </PageColumn>
  );
}
