import type { Metadata } from "next";
import { notFound, redirect } from "next/navigation";
import { StageCloseForm } from "@/components/stage-close-form";
import { ButtonLink } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { ErrorState } from "@/components/ui/states";
import { apiWithSession } from "@/lib/api/server";
import { isId } from "@/lib/ids";
import { formatDate, formatDates } from "@/lib/league";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "Close the Stage" };

/**
 * Closing the current Stage (#10, wireframe M6), for Managers. Games nobody
 * scored must be settled first; then the Manager orders any tie that decides
 * who moves, ticks the Teams waiting for a place, checks the next Stage's
 * dates, and confirms.
 */
export default async function CloseStagePage({ params, searchParams }: PageProps<"/manage/leagues/[leagueId]/close">) {
  const [viewer, api, { leagueId }, { error }] = await Promise.all([getViewer(), apiWithSession(), params, searchParams]);
  if (!viewer.roles.includes("Manager") || !isId(leagueId)) {
    notFound();
  }

  const { data: close, response } = await api.GET("/leagues/{leagueId}/close", { params: { path: { leagueId } } });
  if (response.status === 404) {
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
  const name = (players: { name: string }[]) => players.map((player) => player.name).join(" & ");
  const competitors = close.divisions.flatMap((division) => division.competitors);
  const teamName = (teamId: string) => name(competitors.find((competitor) => competitor.teamId === teamId)?.players ?? []);
  const unscored = close.divisions.flatMap((division) =>
    division.games.filter((game) => close.awaiting.unscoredGameIds.includes(game.id)),
  );

  return (
    <>
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-bold">Close Stage {Number(stage.number)}</h1>
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

      {unscored.length > 0 ? (
        <Card title="Settle these first" aria-label="Settle these first">
          <p className="mb-2 text-sm text-fg-muted">
            A Stage closes only when every game has a score or is marked not played, so nobody moves on a missing result.
          </p>
          <ul aria-label="Games with no score" className="divide-y divide-border text-sm">
            {unscored.map((game) => (
              <li key={game.id} className="flex items-center justify-between gap-2 py-1.5">
                <span className="min-w-0 break-words">
                  {teamName(game.firstTeamId)} v {teamName(game.secondTeamId)}
                </span>
                <ButtonLink href={`/games/${game.id}`} variant="secondary" className="shrink-0">
                  Settle
                </ButtonLink>
              </li>
            ))}
          </ul>
        </Card>
      ) : (
        <StageCloseForm
          leagueId={leagueId}
          stageNumber={Number(stage.number)}
          stageEnd={stage.endDate}
          leagueEnd={close.leagueEndDate}
          divisions={close.divisions.map((division) => ({
            number: Number(division.number),
            teams: division.standings.map((standing) => {
              const difference = Number(standing.gamesFor) - Number(standing.gamesAgainst);
              return {
                teamId: standing.teamId,
                name: teamName(standing.teamId),
                rank: Number(standing.rank),
                record: `${Number(standing.won)}–${Number(standing.lost)}, ${difference > 0 ? "+" : ""}${difference}`,
              };
            }),
            ties: close.awaiting.ties.filter((tie) => Number(tie.division) === Number(division.number)).map((tie) => tie.teamIds),
          }))}
          waiting={close.waiting.map((team) => ({ teamId: team.teamId, name: name(team.players) }))}
          nextStage={close.nextStage ?? null}
        />
      )}

      <div>
        <ButtonLink href={`/manage/leagues/${leagueId}`} variant="secondary">
          Back to the league
        </ButtonLink>
      </div>
    </>
  );
}
