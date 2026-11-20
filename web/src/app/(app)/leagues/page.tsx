import type { Metadata } from "next";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { SubmitButton } from "@/components/ui/submit-button";
import { askToJoinLeague } from "@/lib/actions";
import { apiWithSession } from "@/lib/api/server";
import type { components } from "@/lib/api/schema";
import { describeScoreFormat, describeStructure, formatDate, formatDates, typeLabel } from "@/lib/league";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "Leagues" };

type PlayerLeague = components["schemas"]["PlayerLeague"];

function LeagueFacts({ league }: { league: PlayerLeague }) {
  return (
    <>
      <p className="text-sm text-fg-muted">
        {typeLabel(league.type)} · {describeScoreFormat(league.scoreFormat)}
      </p>
      <p className="text-sm text-fg-muted">
        {describeStructure(league)} · {formatDates(league.startDate, league.endDate)}
      </p>
    </>
  );
}

/** What the Player can do about a League they aren't in, or what became of asking. */
function JoinState({ league, playerId }: { league: PlayerLeague; playerId: string }) {
  const request = league.joinRequest;
  if (request?.status === "Pending") {
    return (
      <p className="text-sm">
        <Badge tone="warning">Request sent</Badge>{" "}
        <span className="text-fg-muted">Asked {formatDate(request.requestedAt.slice(0, 10))}. A manager decides.</span>
      </p>
    );
  }
  if (request?.status === "Rejected") {
    return (
      <p className="text-sm">
        <Badge tone="danger">Not accepted</Badge>{" "}
        <span className="text-fg-muted">A manager turned down this request. Ask them if you think it&apos;s a mistake.</span>
      </p>
    );
  }
  return (
    <form action={askToJoinLeague}>
      <input type="hidden" name="leagueId" value={league.id} />
      <input type="hidden" name="playerId" value={playerId} />
      <SubmitButton pendingLabel="Asking…" aria-label={`Ask to join ${league.name}`}>
        Ask to join
      </SubmitButton>
    </form>
  );
}

/**
 * Leagues (wireframe P6), for the Acting Player: the ones they're in, then the
 * ones at their Clubs they could join, with the state of any request. Each
 * League opens its ladder once #7 starts it.
 */
export default async function LeaguesPage() {
  const [viewer, api] = await Promise.all([getViewer(), apiWithSession()]);
  const { actingPlayer } = viewer;

  if (!actingPlayer) {
    return (
      <>
        <h1 className="text-2xl font-bold">Leagues</h1>
        <EmptyState title="Say who you are first" description="Save your name on Home, then join a club and its leagues." />
      </>
    );
  }

  const { data } = await api.GET("/players/{playerId}/leagues", { params: { path: { playerId: actingPlayer.id } } });
  if (!data) {
    return (
      <>
        <h1 className="text-2xl font-bold">Leagues</h1>
        <ErrorState title="Leagues didn't load" description="The server isn't reachable right now. Try again shortly." />
      </>
    );
  }

  const mine = data.leagues.filter((league) => league.joined);
  const joinable = data.leagues.filter((league) => !league.joined);
  const inAClub = actingPlayer.clubs.some((club) => club.approved);

  return (
    <>
      <h1 className="text-2xl font-bold">Leagues</h1>

      <section aria-labelledby="mine" className="flex flex-col gap-3">
        <h2 id="mine" className="text-base font-semibold">
          {actingPlayer.name}&apos;s leagues
        </h2>
        {mine.length === 0 ? (
          <EmptyState
            title="Not in a league yet"
            description="Ask to join one below, or wait for a manager to add you."
          />
        ) : (
          mine.map((league) => (
            <Card key={league.id} title={league.name} aria-label={league.name} aside={<Badge tone="accent">Joined</Badge>}>
              <LeagueFacts league={league} />
              {["DoublesMen", "DoublesWomen", "DoublesMix"].includes(league.type) && (
                <p className="mt-2 text-sm">
                  {league.partnerName ? (
                    <>
                      Partner: <strong>{league.partnerName}</strong>
                    </>
                  ) : (
                    "No partner yet. The manager pairs players into teams."
                  )}
                </p>
              )}
              <p className="mt-2 text-sm text-fg-muted">
                {league.participantCount} players. The ladder appears when the manager starts the league.
              </p>
            </Card>
          ))
        )}
      </section>

      <section aria-labelledby="joinable" className="flex flex-col gap-3">
        <h2 id="joinable" className="text-base font-semibold">
          You can join
        </h2>
        {joinable.length === 0 ? (
          <EmptyState
            title={inAClub ? "Nothing else to join" : "Join a club first"}
            description={
              inAClub
                ? "When a manager creates a league at your club, it shows here."
                : "Leagues belong to a club. Ask to join one on Home."
            }
          />
        ) : (
          joinable.map((league) => (
            <Card key={league.id} title={league.name} aria-label={league.name}>
              <div className="flex flex-col gap-3">
                <div>
                  <LeagueFacts league={league} />
                </div>
                <JoinState league={league} playerId={actingPlayer.id} />
              </div>
            </Card>
          ))
        )}
        {viewer.players.length > 1 && (
          <p className="text-sm text-fg-muted">
            Showing {actingPlayer.name}. Switch player at the top to see someone else&apos;s leagues.
          </p>
        )}
      </section>
    </>
  );
}
