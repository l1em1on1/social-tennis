import { PlayerForm } from "@/components/player-form";
import { HeldUpNotice, owedGames, PlayerLeagueGames } from "@/components/player-league-games";
import { ButtonLink } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { apiWithSession } from "@/lib/api/server";
import { getViewer } from "@/lib/viewer";

/**
 * Home (#42, wireframe P2): the Acting Player's Games this Stage in each of
 * their Leagues, the Scores they still owe, the deadlines, and any Stage held
 * up. It is also where a new User says who they are. There's no list of Clubs
 * to join (#222): a Player asks through their Club's join link, or a Manager
 * adds them, so a Player in no Club yet is told how to get in.
 */
export default async function Home({ searchParams }: PageProps<"/">) {
  const [viewer, api, { error: formError }] = await Promise.all([getViewer(), apiWithSession(), searchParams]);
  const { actingPlayer } = viewer;

  const inAClub = actingPlayer?.clubs.some((club) => club.approved) ?? false;
  const waitingOn = actingPlayer?.clubs.filter((club) => !club.approved).map((club) => club.name) ?? [];
  // Switching the Acting Player switches all of it.
  const { data: games, error: gamesError } =
    actingPlayer && inAClub
      ? await api.GET("/players/{playerId}/games", { params: { path: { playerId: actingPlayer.id } } })
      : { data: undefined, error: undefined };
  const owed = (games?.leagues ?? []).flatMap(owedGames).length;

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

      {actingPlayer && !inAClub && (
        // Not in a Club yet: how to get in (#222).
        <Card title={waitingOn.length > 0 ? "Waiting to join" : "Join your club"}>
          <p className="text-fg-muted">
            {waitingOn.length > 0
              ? `${actingPlayer.name}'s request to join ${waitingOn.join(" and ")} is with the club's manager. Once you're in, your leagues and games show here.`
              : `Ask your club's manager for the club's join link, or to add ${viewer.email}. Once you're in, your leagues and games show here.`}
          </p>
        </Card>
      )}

      {actingPlayer &&
        inAClub &&
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

      <p className="text-sm text-fg-muted">Signed in as {viewer.email}</p>
    </>
  );
}
