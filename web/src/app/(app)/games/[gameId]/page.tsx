import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { ResultStatusBadge } from "@/components/result-status-badge";
import { ScoreHistory } from "@/components/score-history";
import { ScoreTable } from "@/components/score-table";
import { ButtonLink } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { apiWithSession } from "@/lib/api/server";
import { isId } from "@/lib/ids";
import { describeScoreFormat, formatDate } from "@/lib/league";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "Match" };

/** What saving said, for the Match screen it lands on. */
const SAVED: Record<string, string> = {
  enter: "Score saved. Everyone in the league can see it now.",
  correct: "Correction saved: the score is as shown, and its history keeps every change.",
  override: "Override saved: the score is as shown, and its history keeps every change.",
};

/**
 * One League Game (#38, wireframe P3): both Sides, its Score set by set once
 * entered, who entered it and every change since; until then, the deadline
 * and, for a Player in it, the way to enter it. A Player in it corrects it
 * while the Stage is open; a Manager overrides it at any time; after the
 * close, its Players see it locked. The League's Players and Managers see
 * it; anyone else gets a 404.
 */
export default async function MatchPage({ params, searchParams }: PageProps<"/games/[gameId]">) {
  const [viewer, api, { gameId }, { saved, refused }] = await Promise.all([
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
  const manager = viewer.roles.includes("Manager");
  const { score, stage } = game;
  const stageName = `Stage ${stage.number}`;
  const refusals: Record<string, string> = {
    "already-scored": "Someone entered this game's score first. Here it is.",
    "not-in-game": "Only the players in this game can enter its score.",
    "stage-closed": `${stageName} has closed, so its players can't change this game's score any more.`,
    "not-scored": "This game has no score to change yet.",
  };
  const savedMessage = typeof saved === "string" ? SAVED[saved] : undefined;
  const refusal = typeof refused === "string" ? refusals[refused] : undefined;

  return (
    <>
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-bold break-words">
          {sides[0]} v {sides[1]}
        </h1>
        <p className="text-fg-muted">
          {game.leagueName} · Division {game.division} · {stageName}
          {stage.closed ? " (closed)" : ""}
        </p>
      </div>

      {savedMessage && score && (
        <p role="status" className="rounded-2xl border border-success bg-success-muted px-4 py-3 text-sm">
          {savedMessage}
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
          <div className="mt-3 flex flex-col gap-2">
            {playing && !stage.closed ? (
              <ButtonLink href={`/games/${game.id}/score`} variant="secondary" fullWidth>
                Correct the score
              </ButtonLink>
            ) : manager ? (
              <>
                <ButtonLink href={`/games/${game.id}/score`} variant="secondary" fullWidth>
                  Override score (manager)
                </ButtonLink>
                {stage.closed && (
                  <p className="text-sm text-fg-muted">{stageName} is closed. An override is kept in the score&apos;s history.</p>
                )}
              </>
            ) : (
              playing && (
                <p role="note" className="rounded-xl border border-border bg-surface-muted px-3 py-2 text-sm">
                  <strong>🔒 {stageName} is closed.</strong> This score can&apos;t be changed any more. If it&apos;s wrong, ask the
                  manager to override it.
                </p>
              )
            )}
          </div>
        </Card>
      ) : (
        <Card title="No score yet" aria-label="No score yet" aside={<ResultStatusBadge status="pending" />}>
          <div className="flex flex-col gap-3">
            {stage.closed ? (
              <p className="text-sm">{stageName} has closed without a score for this game: the manager settles it.</p>
            ) : (
              <p className="text-sm">
                Play by <strong>{formatDate(stage.endDate)}</strong>. Anyone who plays in it can enter the score.
              </p>
            )}
            {playing && !stage.closed ? (
              <ButtonLink href={`/games/${game.id}/score`} fullWidth>
                Enter score
              </ButtonLink>
            ) : (
              acting &&
              !playing && (
                <p className="text-sm text-fg-muted">{acting.name} doesn&apos;t play in this game, so can&apos;t enter its score.</p>
              )
            )}
          </div>
        </Card>
      )}

      {game.history.length > 0 && (
        <Card title="History" aria-label="History">
          <ScoreHistory history={game.history} sides={sides} timeZone={game.timeZone} />
          <p className="mt-3 text-sm text-fg-muted">Every change stays here: who, when, and from what to what.</p>
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
