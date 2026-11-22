import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { ResultStatusBadge } from "@/components/result-status-badge";
import { ScoreTable } from "@/components/score-table";
import { ButtonLink } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { apiWithSession } from "@/lib/api/server";
import { isId } from "@/lib/ids";
import { describeScoreFormat, formatDate } from "@/lib/league";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "Match" };

/** What the score form's refusals mean, for whoever it sent back here. */
const REFUSED: Record<string, string> = {
  "already-scored": "Someone entered this game's score first. Here it is.",
  "not-in-game": "Only the players in this game can enter its score.",
};

/**
 * One League Game (#38, wireframe P3): both Sides, its Score set by set once
 * entered, and who entered it; until then, the deadline and, for a Player in
 * it, the way to enter it. The League's Players and Managers see it; anyone
 * else gets a 404.
 */
export default async function MatchPage({ params, searchParams }: PageProps<"/games/[gameId]">) {
  const [viewer, api, { gameId }, { entered, refused }] = await Promise.all([
    getViewer(),
    apiWithSession(),
    params,
    searchParams,
  ]);
  if (!isId(gameId)) {
    notFound();
  }

  const { data: game, response } = await api.GET("/games/{gameId}", { params: { path: { gameId } } });
  if (response.status === 404) {
    notFound();
  }
  if (!game) {
    throw new Error(`GET /games/${gameId} failed with ${response.status}`);
  }

  const sides = game.sides.map((side) => side.players.map((player) => player.name).join(" & ")) as [string, string];
  const acting = viewer.actingPlayer;
  const playing = acting ? game.yourPlayerIds.includes(acting.id) : false;
  const { score } = game;
  const refusal = typeof refused === "string" ? REFUSED[refused] : undefined;

  return (
    <>
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-bold break-words">
          {sides[0]} v {sides[1]}
        </h1>
        <p className="text-fg-muted">
          {game.leagueName} · Division {game.division} · Stage {game.stage.number}
        </p>
      </div>

      {entered === "1" && score && (
        <p role="status" className="rounded-2xl border border-success bg-success-muted px-4 py-3 text-sm">
          Score saved. Everyone in the league can see it now.
        </p>
      )}
      {refusal && (
        <p role="status" className="rounded-2xl border border-border bg-surface-muted px-4 py-3 text-sm">
          {refusal}
        </p>
      )}

      {score ? (
        <Card title="Score" aria-label="Score" aside={<ResultStatusBadge status={score.retired ? "retirement" : "played"} />}>
          <ScoreTable sides={sides} sets={score.sets} winner={Number(score.winner)} />
          <p className="mt-3 text-sm text-fg-muted">
            <strong className="text-fg">{sides[Number(score.winner) - 1]}</strong> won
            {score.retired ? " when the game stopped early" : ""} · played {formatDate(score.playedOn)} · entered by{" "}
            {score.submittedBy}
          </p>
        </Card>
      ) : (
        <Card title="No score yet" aria-label="No score yet" aside={<ResultStatusBadge status="pending" />}>
          <div className="flex flex-col gap-3">
            <p className="text-sm">
              Play by <strong>{formatDate(game.stage.endDate)}</strong>. Anyone who plays in it can enter the score.
            </p>
            {playing ? (
              <ButtonLink href={`/games/${game.id}/score`} fullWidth>
                Enter score
              </ButtonLink>
            ) : (
              acting && (
                <p className="text-sm text-fg-muted">{acting.name} doesn&apos;t play in this game, so can&apos;t enter its score.</p>
              )
            )}
          </div>
        </Card>
      )}

      <Card title="Format">
        <p className="text-sm text-fg-muted">{describeScoreFormat(game.scoreFormat)}.</p>
      </Card>

      <div>
        <ButtonLink href={`/leagues/${game.leagueId}`} variant="secondary">
          Back to the ladder
        </ButtonLink>
      </div>
    </>
  );
}
