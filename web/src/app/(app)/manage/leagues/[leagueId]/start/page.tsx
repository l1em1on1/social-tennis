import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { LeagueStartForm } from "@/components/league-start-form";
import { ButtonLink } from "@/components/ui/button";
import { ErrorState } from "@/components/ui/states";
import { apiWithSession } from "@/lib/api/server";
import { isId } from "@/lib/ids";
import { typeLabel } from "@/lib/league";
import { getViewer } from "@/lib/viewer";
import { PageColumn } from "@/components/shell/page-column";

export const metadata: Metadata = { title: "Start the league" };

/** Why a League can't start, by the API's 409 type. */
const CANT_START: Record<string, string> = {
  "already-started": "This league has already started.",
  "knockout-not-yet": "Starting a knockout league isn't built yet.",
  "foursomes-not-yet": "Starting a Foursomes league isn't built yet.",
};

/**
 * Start a Divisional League (#7, wireframe M4): the proposed starting ladder,
 * by Rating, then whoever joined first (ADR-0013), for the Manager to reorder,
 * cut and confirm. Every Rating is 1 while Rating is hidden (#161), so in
 * practice the proposal is the order Players joined.
 */
export default async function StartLeaguePage({ params, searchParams }: PageProps<"/manage/leagues/[leagueId]/start">) {
  const [viewer, api, { leagueId }, { error }] = await Promise.all([getViewer(), apiWithSession(), params, searchParams]);
  if (!viewer.manages || !isId(leagueId)) {
    notFound();
  }

  const { data: league, response } = await api.GET("/leagues/{leagueId}", { params: { path: { leagueId } } });
  // 403: a League they can see, but don't run (#110).
  if (response.status === 404 || response.status === 403) {
    notFound();
  }
  if (!league) {
    throw new Error(`GET /leagues/${leagueId} failed with ${response.status}`);
  }
  const back = (
    <ButtonLink href={`/manage/leagues/${league.id}`} variant="secondary">
      Back to {league.name}
    </ButtonLink>
  );

  const start = await api.GET("/leagues/{leagueId}/start", { params: { path: { leagueId } } });
  if (!start.data) {
    const reason = start.response.status === 409 ? CANT_START[start.error?.type ?? ""] : undefined;
    if (!reason) {
      throw new Error(`GET /leagues/${leagueId}/start failed with ${start.response.status}`);
    }
    return (
      <PageColumn>
        <h1 className="text-2xl font-bold break-words">Start {league.name}</h1>
        <ErrorState title="This league can't start" description={reason} />
        <div>{back}</div>
      </PageColumn>
    );
  }

  const { competitors, divisions, divisionSize, unpaired, waitingRequestCount, firstStage } = start.data;
  // The proposal is by Rating, then join time (ADR-0013). Rating isn't shown anywhere (ADR-0007,
  // owner 2026-11-21) and stays 1, so the order is join order; the screen only says so when it is.
  const byJoinTime = new Set(competitors.map((competitor) => Number(competitor.rating))).size <= 1;
  return (
    <PageColumn>
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-bold break-words">Start {league.name}</h1>
        <p className="text-fg-muted">
          {league.clubName} · {typeLabel(league.type)}
        </p>
      </div>
      <p className="text-sm">
        {byJoinTime ? "Proposed in the order players joined" : "Proposed order"}, in Divisions of about {String(divisionSize)}. Move
        anyone up or down, leave them out, or change where the Divisions split, then confirm.
      </p>
      {error === "changed" && (
        <ErrorState
          title="The league changed in the meantime"
          description="Someone was added, paired or removed since this page loaded, so nothing started. This is the ladder as it is now."
        />
      )}
      {error === "ladder" && (
        <ErrorState title="That ladder didn't work" description="Put at least two on the ladder, each once." />
      )}
      {competitors.length < 2 ? (
        <ErrorState
          title="Not enough to start"
          description={
            unpaired.length > 0
              ? "A ladder needs at least two teams. Pair the players without a partner first."
              : "A ladder needs at least two on it. Approve requests or add players first."
          }
        />
      ) : (
        // Keyed by the proposal, so after a refused start the form begins again from the new one.
        <LeagueStartForm
          key={competitors.map((competitor) => competitor.teamId).join() + unpaired.map((player) => player.playerId).join()}
          leagueId={league.id}
          competitors={competitors.map((competitor) => ({
            teamId: competitor.teamId,
            names: competitor.players.map((player) => player.name),
          }))}
          divisions={divisions.map(Number)}
          unpaired={unpaired}
          waitingRequestCount={Number(waitingRequestCount)}
          firstStage={firstStage}
        />
      )}
      <div>{back}</div>
    </PageColumn>
  );
}
