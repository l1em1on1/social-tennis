import type { Metadata } from "next";
import { notFound, redirect } from "next/navigation";
import { GamesToSettle } from "@/components/games-to-settle";
import { StageCloseForm } from "@/components/stage-close-form";
import { ButtonLink } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { ErrorState } from "@/components/ui/states";
import { apiWithSession } from "@/lib/api/server";
import { isId } from "@/lib/ids";
import { formatDate, formatDates, gameLabel } from "@/lib/league";
import { getViewer } from "@/lib/viewer";
import { PageColumn } from "@/components/shell/page-column";

export const metadata: Metadata = { title: "Close the Stage" };

/** A date the close form typed, carried back through a refusal: only a well-formed one. */
const typed = (value: string | string[] | undefined) => (typeof value === "string" && /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : null);

/**
 * Closing the current Stage (#10, wireframe M6), for Managers. Games nobody
 * scored must be settled first; then the Manager orders any tie that decides
 * who moves, ticks the Teams waiting for a place, checks the next Stage's
 * dates, and confirms.
 */
export default async function CloseStagePage({ params, searchParams }: PageProps<"/manage/leagues/[leagueId]/close">) {
  const [viewer, api, { leagueId }, { error, start, end }] = await Promise.all([getViewer(), apiWithSession(), params, searchParams]);
  if (!viewer.manages || !isId(leagueId)) {
    notFound();
  }

  const { data: close, response } = await api.GET("/leagues/{leagueId}/close", { params: { path: { leagueId } } });
  // 403: a League they can see, but don't run (#110).
  if (response.status === 404 || response.status === 403) {
    notFound();
  }
  if (response.status === 409) {
    // Its last Stage has closed: the League has ended.
    redirect(`/manage/leagues/${leagueId}`);
  }
  if (!close) {
    throw new Error(`GET /leagues/${leagueId}/close failed with ${response.status}`);
  }

  const { stage } = close;
  // One tie can be both open and half marked (level for first and last): list it once.
  const sameTeams = (a: string[], b: string[]) => [...a].sort().join() === [...b].sort().join();
  const name = (players: { name: string }[]) => players.map((player) => player.name).join(" & ");
  const competitors = close.divisions.flatMap((division) => division.competitors);
  // Only a Foursomes Game has pairs (#14, ADR-0036), and every one of them does.
  const foursomes = close.divisions.some((division) => division.games.some((game) => game.firstPartnerTeamId));
  const teamName = (teamId: string) => name(competitors.find((competitor) => competitor.teamId === teamId)?.players ?? []);
  const unscored = close.divisions.flatMap((division) =>
    division.games
      .filter((game) => close.awaiting.unscoredGameIds.includes(game.id))
      .map((game) => ({
        id: game.id,
        division: Number(division.number),
        label: gameLabel(game, teamName),
      })),
  );

  return (
    <PageColumn>
      <div className="flex flex-col gap-1">
        <h1 className="text-4xl leading-none font-black">Close Stage {Number(stage.number)}</h1>
        <p className="text-fg-muted">
          {formatDates(stage.startDate, stage.endDate)}
          {close.heldUp ? `. It ended on ${formatDate(stage.endDate)}.` : `. It runs to ${formatDate(stage.endDate)}: closing early is fine.`}
        </p>
      </div>

      {error === "changed" && (
        <ErrorState
          title="Things changed while you were looking"
          description="A score was entered or changed, or a Team waiting for a place went. This is how it stands now: check it and close again."
        />
      )}
      {error === "queue" && <ErrorState title="A game still has no score" description="Settle it below, then close the Stage." />}
      {error === "withdrawn" && (
        <ErrorState
          title="A team that has withdrawn is still on the ladder"
          description="Closing a Stage with one, and naming a replacement, is still to be built (#21)."
        />
      )}

      {unscored.length > 0 ? (
        <Card title="Settle these first" aria-label="Settle these first">
          <p className="mb-2 text-sm text-fg-muted">
            A Stage closes only when every game has a score or is marked not played, so nobody moves on a missing result.
          </p>
          <GamesToSettle games={unscored} />
        </Card>
      ) : (
        <StageCloseForm
          // Rebuilt from what it shows: after a "things changed" refusal the URL changes only its query, so
          // Next keeps the form mounted, and its order and ticks must start again from the new data (#217 review).
          key={JSON.stringify([close.divisions.map((division) => division.standings), close.waiting])}
          leagueId={leagueId}
          stageNumber={Number(stage.number)}
          stageEnd={stage.endDate}
          leagueEnd={close.leagueEndDate}
          divisions={close.divisions.map((division) => ({
            number: Number(division.number),
            teams: division.standings.map((standing) => {
              const difference = Number(standing.gamesFor) - Number(standing.gamesAgainst);
              // What the Players said of a Foursomes tie (#14 part 3): shown, and in order, for the Manager to keep or change.
              const marked = division.playersTies.find((tie) => tie.mark?.teamId === standing.teamId);
              return {
                teamId: standing.teamId,
                name: teamName(standing.teamId),
                rank: Number(standing.rank),
                // Points first where they rank (#214): "5 pts, 2–0, +5". In Foursomes, the games banked that rank them (#14).
                record:
                  foursomes
                    ? `${Number(standing.gamesFor)} games${marked ? ` · the players say ${marked.place === "Up" ? "up" : "down"}` : ""}`
                    : `${standing.points != null ? `${Number(standing.points)} pts, ` : ""}${Number(standing.won)}–${Number(standing.lost)}, ${difference > 0 ? "+" : ""}${difference}`,
              };
            }),
            // Ties still open, and those the Players marked, which the Manager can still reorder.
            ties: [
              ...close.awaiting.ties.filter((tie) => Number(tie.division) === Number(division.number)).map((tie) => tie.teamIds),
              ...division.playersTies.filter((tie) => tie.mark).map((tie) => tie.teamIds),
            ].filter((tie, index, all) => all.findIndex((other) => sameTeams(other, tie)) === index),
          }))}
          waiting={close.waiting.map((team) => ({ teamId: team.teamId, name: name(team.players) }))}
          nextStage={
            close.nextStage
              ? { startDate: typed(start) ?? close.nextStage.startDate, endDate: typed(end) ?? close.nextStage.endDate }
              : null
          }
        />
      )}

      <div>
        <ButtonLink href={`/manage/leagues/${leagueId}`} variant="secondary">
          Back to the league
        </ButtonLink>
      </div>
    </PageColumn>
  );
}
