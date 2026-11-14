import { PlayerNameForm } from "@/components/player-name-form";
import { Badge } from "@/components/ui/badge";
import { ButtonLink } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { SubmitButton } from "@/components/ui/submit-button";
import { joinClub } from "@/lib/actions";
import { apiWithSession } from "@/lib/api/server";
import { getViewer } from "@/lib/viewer";

/**
 * Home. #42 turns this into "this month's Games, deadline, Scores owed"; until
 * Leagues exist it is where a new User says who they are and joins a Club.
 */
export default async function Home({ searchParams }: PageProps<"/">) {
  const [viewer, api, { error: formError }] = await Promise.all([getViewer(), apiWithSession(), searchParams]);
  const { actingPlayer } = viewer;

  // List endpoints return an envelope, not the array itself (ADR-0012) — the
  // collection is one property of it, alongside `page`.
  const { data: clubList, error } = await api.GET("/clubs");
  const joined = new Set(actingPlayer?.clubs.map((club) => club.id));

  return (
    <>
      <h1 className="text-2xl font-bold">Home</h1>

      {/* First sign-in: nobody to play as yet. Not a gate: a login that only runs leagues can leave it. */}
      {!actingPlayer && (
        <Card title="Welcome! What's your name?">
          {formError === "name" && (
            <div className="mb-4">
              <ErrorState title="That name didn't work" description="Enter a name of up to 100 characters." />
            </div>
          )}
          <PlayerNameForm
            id="welcome-name"
            label="Your name"
            hint="The name other players see on ladders and scores. You can change it later in Profiles."
            submitLabel="Save my name"
            from="/"
          />
        </Card>
      )}

      {actingPlayer && (
        <Card title="This month">
          <EmptyState
            title="No games yet"
            description={`${actingPlayer.name}'s games appear here once a league they're in starts.`}
            action={
              <ButtonLink href="/ladder" variant="secondary">
                View the ladder
              </ButtonLink>
            }
          />
        </Card>
      )}

      {error || !clubList ? (
        <ErrorState title="Clubs didn't load" description="The server isn't reachable right now. Try again shortly." />
      ) : (
        <Card title="Clubs">
          {clubList.clubs.length === 0 ? (
            <EmptyState title="No clubs yet" description="An Admin sets up the club." />
          ) : (
            <>
              {!actingPlayer && <p className="mb-2 text-sm text-fg-muted">Save your name above to join a club.</p>}
              <ul className="-my-2 divide-y divide-border">
                {clubList.clubs.map((club) => (
                  <li key={club.id} className="flex min-h-14 items-center justify-between gap-3 py-2">
                    <span className="font-medium">{club.name}</span>
                    {joined.has(club.id) ? (
                      <Badge tone="accent">Member</Badge>
                    ) : (
                      actingPlayer && (
                        <form action={joinClub}>
                          <input type="hidden" name="clubId" value={club.id} />
                          <SubmitButton variant="secondary" pendingLabel="Joining…" aria-label={`Join ${club.name}`}>
                            Join
                          </SubmitButton>
                        </form>
                      )
                    )}
                  </li>
                ))}
              </ul>
              {actingPlayer && viewer.players.length > 1 && (
                <p className="mt-3 text-sm text-fg-muted">
                  Joining as {actingPlayer.name}. Switch player at the top to join as someone else.
                </p>
              )}
            </>
          )}
        </Card>
      )}

      <p className="text-sm text-fg-muted">Signed in as {viewer.email}</p>
    </>
  );
}
