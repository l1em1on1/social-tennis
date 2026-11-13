import type { Metadata } from "next";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/states";
import { logout, switchActingPlayer } from "@/lib/actions";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "Profiles" };

/** The Players on this login, which one is active, and sign-out. */
export default async function ProfilesPage() {
  const viewer = await getViewer();

  return (
    <>
      <h1 className="text-2xl font-bold">Profiles</h1>

      <Card title="Players on this login">
        {viewer.players.length === 0 ? (
          <EmptyState
            title="No player profile"
            description="This login runs leagues but doesn't play in them."
          />
        ) : (
          <ul className="-my-2 divide-y divide-border">
            {viewer.players.map((player) => {
              const acting = player.id === viewer.actingPlayer?.id;
              return (
                <li key={player.id} className="flex min-h-14 items-center justify-between gap-3 py-2">
                  <span className="font-medium">{player.name}</span>
                  {acting ? (
                    <Badge tone="accent">Playing as</Badge>
                  ) : (
                    <form action={switchActingPlayer}>
                      <input type="hidden" name="playerId" value={player.id} />
                      <Button type="submit" variant="secondary">
                        Play as {player.name.split(" ")[0]}
                      </Button>
                    </form>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </Card>

      <Card title="Account">
        <p className="mb-3 text-sm text-fg-muted">Signed in as {viewer.email}</p>
        <form action={logout}>
          <Button type="submit" variant="secondary" fullWidth>
            Log out
          </Button>
        </form>
      </Card>
    </>
  );
}
