import { PlayerForm } from "@/components/player-form";
import { HeldUpNotice, owedGames, PlayerLeagueGames } from "@/components/player-league-games";
import { Badge } from "@/components/ui/badge";
import { ButtonLink } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { SubmitButton } from "@/components/ui/submit-button";
import { joinClub } from "@/lib/actions";
import { apiWithSession } from "@/lib/api/server";
import { getViewer } from "@/lib/viewer";

/**
 * Home (#42, wireframe P2): the Acting Player's Games this Stage in each of
 * their Leagues, the Scores they still owe, the deadlines, and any Stage held
 * up. It is also where a new User says who they are, and joins a Club.
 */
export default async function Home({ searchParams }: PageProps<"/">) {
  const [viewer, api, { error: formError }] = await Promise.all([getViewer(), apiWithSession(), searchParams]);
  const { actingPlayer } = viewer;

  // List endpoints return an envelope, not the array itself (ADR-0012) — the
  // collection is one property of it, alongside `page`.
  const { data: clubList, error } = await api.GET("/clubs");
  // Switching the Acting Player switches all of it.
  const { data: games, error: gamesError } = actingPlayer
    ? await api.GET("/players/{playerId}/games", { params: { path: { playerId: actingPlayer.id } } })
    : { data: undefined, error: undefined };
  const owed = (games?.leagues ?? []).flatMap(owedGames).length;
  const membership = new Map(actingPlayer?.clubs.map((club) => [club.id, club]));

  return (
    <>
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-bold">Home</h1>
        {games && games.leagues.length > 0 && (
          <p className="text-fg-muted">
            {actingPlayer?.name} · {owed === 0 ? "no scores owed" : `${owed} ${owed === 1 ? "score" : "scores"} needed`}
          </p>
        )}
      </div>

      {/* First sign-in: nobody to play as yet. Not a gate: a login that only runs leagues can leave it. */}
      {!actingPlayer && (
        <Card title="Welcome! What's your name?">
          {formError === "name" && (
            <div className="mb-4">
              <ErrorState title="That name didn't work" description="Enter a name of up to 100 characters." />
            </div>
          )}
          <PlayerForm
            id="welcome-name"
            label="Your name"
            hint="The name other players see on ladders and scores. You can change it, and add your level, in Profiles."
            submitLabel="Save my name"
            from="/"
          />
        </Card>
      )}

      {actingPlayer &&
        (gamesError || !games ? (
          <ErrorState title="Your games didn't load" description="The server isn't reachable right now. Try again shortly." />
        ) : games.leagues.length === 0 ? (
          <EmptyState
            title="No games yet"
            description={`${actingPlayer.name}'s games appear here once a league they're in starts. Ask to join one, or wait for the manager to add you.`}
            action={
              <ButtonLink href="/leagues" variant="secondary">
                See leagues
              </ButtonLink>
            }
          />
        ) : (
          <>
            {/* Above everything, so Players see what holds a Stage up without asking (wireframe P2). */}
            {games.leagues
              .filter((league) => league.heldUp)
              .map((league) => (
                <HeldUpNotice key={league.leagueId} league={league} />
              ))}
            <ul aria-label="Your leagues" className="flex flex-col gap-3">
              {games.leagues.map((league) => (
                <li key={league.leagueId}>
                  <PlayerLeagueGames league={league} />
                </li>
              ))}
            </ul>
          </>
        ))}

      {error || !clubList ? (
        <ErrorState title="Clubs didn't load" description="The server isn't reachable right now. Try again shortly." />
      ) : (
        <Card title="Clubs">
          {clubList.clubs.length === 0 ? (
            <EmptyState title="No clubs yet" description="An Admin sets up the club." />
          ) : (
            <>
              <p className="mb-2 text-sm text-fg-muted">
                {actingPlayer
                  ? "A club manager approves each request to join."
                  : "Save your name above to ask to join a club."}
              </p>
              <ul className="-my-2 divide-y divide-border">
                {clubList.clubs.map((club) => (
                  <li key={club.id} className="flex min-h-14 items-center justify-between gap-3 py-2">
                    <span className="font-medium">{club.name}</span>
                    {membership.get(club.id)?.approved ? (
                      <Badge tone="accent">Member</Badge>
                    ) : membership.has(club.id) ? (
                      <Badge>Waiting for approval</Badge>
                    ) : (
                      actingPlayer && (
                        <form action={joinClub}>
                          <input type="hidden" name="clubId" value={club.id} />
                          <input type="hidden" name="playerId" value={actingPlayer.id} />
                          <SubmitButton variant="secondary" pendingLabel="Asking…" aria-label={`Ask to join ${club.name}`}>
                            Ask to join
                          </SubmitButton>
                        </form>
                      )
                    )}
                  </li>
                ))}
              </ul>
              {actingPlayer && viewer.players.length > 1 && (
                <p className="mt-3 text-sm text-fg-muted">
                  Asking as {actingPlayer.name}. Switch player at the top to ask as someone else.
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
