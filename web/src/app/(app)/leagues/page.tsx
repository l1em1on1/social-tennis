import type { Metadata } from "next";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Combobox } from "@/components/ui/combobox";
import { Field } from "@/components/ui/field";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { SubmitButton } from "@/components/ui/submit-button";
import { askToJoinLeague, takeBackLeagueJoinRequest, type JoinRefusal } from "@/lib/actions";
import { apiWithSession } from "@/lib/api/server";
import type { components } from "@/lib/api/schema";
import { describeScoreFormat, describeStructure, formatDate, formatDates, typeLabel } from "@/lib/league";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "Leagues" };

type PlayerLeague = components["schemas"]["PlayerLeague"];
type LeaguePartner = components["schemas"]["LeaguePartner"];

const isDoubles = (league: PlayerLeague) => ["DoublesMen", "DoublesWomen", "DoublesMix"].includes(league.type);

/** Why asking with a partner didn't go through (#6), from `askToJoinLeague`. */
const REFUSALS: Record<JoinRefusal, string> = {
  "partner-in-a-team": "That partner already has a partner in this league.",
  "partner-in-a-request":
    "That partner is already in a request to join this league, their own or someone else's. It has to be decided or taken back first.",
  "named-as-partner": "Someone has already asked to join this league with you. Take that back first to ask yourself.",
  "partner-gone": "That partner isn't a member of the club any more. Pick someone else.",
};

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

function TakeBack({ league, askerId, label }: { league: PlayerLeague; askerId: string; label: string }) {
  return (
    <form action={takeBackLeagueJoinRequest}>
      <input type="hidden" name="leagueId" value={league.id} />
      <input type="hidden" name="playerId" value={askerId} />
      <SubmitButton variant="secondary" pendingLabel="Taking back…" aria-label={label}>
        Take back
      </SubmitButton>
    </form>
  );
}

/**
 * A waiting request in which someone named this Player as their partner (#6),
 * with Take back. Shown wherever the Player's card is: they may already be in
 * the League without a partner, or have been turned down themselves.
 */
function NamedAsPartner({ league }: { league: PlayerLeague }) {
  const named = league.partnerRequest;
  if (!named) return null;
  return (
    <div className="flex flex-col gap-2">
      <p className="text-sm">
        <Badge tone="warning">Asked with you</Badge>{" "}
        <span className="text-fg-muted">
          {named.name} asked to join with you as partners {formatDate(named.requestedAt.slice(0, 10))}. A manager decides.
          If you&apos;re not playing together, take it back.
        </span>
      </p>
      <TakeBack league={league} askerId={named.playerId} label={`Take back ${named.name}'s request to join ${league.name} with you`} />
    </div>
  );
}

/**
 * What the Player can do about a League they aren't in, or what became of
 * asking. In a doubles League they may name a partner (#6), and someone may
 * have named them: either of the two can take that request back.
 */
function JoinState({
  league,
  playerId,
  partners,
}: {
  league: PlayerLeague;
  playerId: string;
  partners: LeaguePartner[] | undefined;
}) {
  const request = league.joinRequest;
  const asked = request?.partnerName ? ` with ${request.partnerName}` : "";
  if (request?.status === "Pending") {
    return (
      <div className="flex flex-col gap-2">
        <p className="text-sm">
          <Badge tone="warning">Request sent</Badge>{" "}
          <span className="text-fg-muted">
            Asked{asked} {formatDate(request.requestedAt.slice(0, 10))}. A manager decides.
          </span>
        </p>
        <TakeBack league={league} askerId={playerId} label={`Take back the request to join ${league.name}`} />
      </div>
    );
  }
  if (request?.status === "Rejected") {
    return (
      <div className="flex flex-col gap-2">
        <p className="text-sm">
          <Badge tone="danger">Not accepted</Badge>{" "}
          <span className="text-fg-muted">A manager turned down this request. Ask them if you think it&apos;s a mistake.</span>
        </p>
        <NamedAsPartner league={league} />
      </div>
    );
  }
  if (league.partnerRequest) {
    return <NamedAsPartner league={league} />;
  }
  return (
    <form action={askToJoinLeague} className="flex flex-col gap-3">
      <input type="hidden" name="leagueId" value={league.id} />
      <input type="hidden" name="playerId" value={playerId} />
      {partners && partners.length > 0 && (
        <Field
          id={`partner-${league.id}`}
          label="Partner (optional)"
          hint="Who you're playing with, if you've agreed. Leave it empty to join alone: the manager pairs you."
        >
          {(control) => (
            <Combobox
              {...control}
              name="partnerId"
              listLabel="Club members"
              placeholder="Type a name…"
              options={partners.map((partner) => ({ value: partner.playerId, label: partner.name }))}
            />
          )}
        </Field>
      )}
      <div>
        <SubmitButton pendingLabel="Asking…" aria-label={`Ask to join ${league.name}`}>
          Ask to join
        </SubmitButton>
      </div>
    </form>
  );
}

/**
 * Leagues (wireframe P6), for the Acting Player: the ones they're in, then the
 * ones at their Clubs they could join, with the state of any request. Each
 * League opens its ladder once #7 starts it.
 */
export default async function LeaguesPage({ searchParams }: PageProps<"/leagues">) {
  const [viewer, api, { error }] = await Promise.all([getViewer(), apiWithSession(), searchParams]);
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
  // Who the Player could ask with, for each doubles League they can still ask
  // to join (#6). Missing, if it didn't load: they can still ask alone.
  const askable = joinable.filter((league) => isDoubles(league) && !league.joinRequest && !league.partnerRequest);
  const partnerLists = await Promise.all(
    askable.map(async (league) => {
      const { data: partners } = await api.GET("/leagues/{leagueId}/partners", {
        params: { path: { leagueId: league.id }, query: { playerId: actingPlayer.id } },
      });
      return [league.id, partners?.partners] as const;
    }),
  );
  const partnersOf = new Map(partnerLists);
  // Own keys only: `in` would also match "toString" and the like.
  const refusal = typeof error === "string" && Object.hasOwn(REFUSALS, error) ? REFUSALS[error as JoinRefusal] : undefined;
  const inAClub = actingPlayer.clubs.some((club) => club.approved);

  return (
    <>
      <h1 className="text-2xl font-bold">Leagues</h1>

      {refusal && <ErrorState title="That request didn't go through" description={refusal} />}

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
              {isDoubles(league) && (
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
              {league.partnerRequest && (
                <div className="mt-3">
                  <NamedAsPartner league={league} />
                </div>
              )}
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
                <JoinState league={league} playerId={actingPlayer.id} partners={partnersOf.get(league.id)} />
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
