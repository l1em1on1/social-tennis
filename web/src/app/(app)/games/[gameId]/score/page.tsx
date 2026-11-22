import type { Metadata } from "next";
import { notFound, redirect } from "next/navigation";
import { ScoreEntryForm, type ScoreEntryMode } from "@/components/score-entry-form";
import { apiWithSession } from "@/lib/api/server";
import { isId } from "@/lib/ids";
import { describeScoreFormat, formatDate } from "@/lib/league";
import { entryOf, scoreFormat } from "@/lib/score";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "Score" };

const HEADINGS: Record<ScoreEntryMode, string> = {
  enter: "Enter score",
  correct: "Correct the score",
  override: "Override the score",
  record: "Record the score",
};

/**
 * Entering, correcting or overriding a Game's Score (#38, wireframe P4). The
 * Acting Player enters it, or corrects it while the Stage is open, if they
 * play in the Game; otherwise a Manager overrides one it has, open or closed,
 * or records one nobody entered while the Stage is open (#10).
 * Anyone else goes back to the Match screen, which says why. A correction or
 * override starts from the Score as it is, and one refused because the
 * Score changed meanwhile comes back here with the new one (ADR-0018).
 */
export default async function ScorePage({ params, searchParams }: PageProps<"/games/[gameId]/score">) {
  const [viewer, api, { gameId }, { changed }] = await Promise.all([getViewer(), apiWithSession(), params, searchParams]);
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
  const acting = viewer.actingPlayer;
  const playing = acting !== null && game.yourPlayerIds.includes(acting.id);
  const { score, stage } = game;
  const manager = viewer.roles.includes("Manager");
  const open = !stage.closed && !game.notPlayed;
  const mode: ScoreEntryMode | null = score
    ? playing && !stage.closed
      ? "correct"
      : manager
        ? "override"
        : null
    : open
      ? playing
        ? "enter"
        : manager
          ? "record"
          : null
      : null;
  if (!mode) {
    redirect(
      `/games/${gameId}?refused=${game.notPlayed && !score ? "not-played" : stage.closed && (playing || manager) ? "stage-closed" : "not-in-game"}`,
    );
  }

  const sides = game.sides.map((side) => side.players.map((player) => player.name).join(" & ")) as [string, string];
  const initial = score
    ? {
        sets: score.sets.map(entryOf),
        playedOn: score.playedOn,
        retirementWinner: score.retired ? (Number(score.winner) as 1 | 2) : undefined,
      }
    : undefined;
  return (
    <>
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-bold">{HEADINGS[mode]}</h1>
        <p className="text-fg-muted break-words">
          {sides[0]} v {sides[1]} · {describeScoreFormat(game.scoreFormat)}
        </p>
        {score && (
          <p className="text-sm text-fg-muted">
            {score.recordedByManager ? "Recorded by a manager" : `Entered by ${score.submittedBy ?? "a player"}`}, played{" "}
            {formatDate(score.playedOn)}.
          </p>
        )}
        {(mode === "enter" || mode === "correct") && viewer.players.length > 1 && (
          <p className="text-sm text-fg-muted">You&apos;re {mode === "enter" ? "entering" : "correcting"} it as {acting!.name}.</p>
        )}
      </div>
      {changed === "1" && (
        <p role="note" className="rounded-2xl border border-warning bg-warning-muted px-4 py-3 text-sm">
          <strong>Someone changed this score while you had it open.</strong> This is it now: make your change again.
        </p>
      )}
      <ScoreEntryForm
        key={score?.version ?? "new"}
        gameId={game.id}
        mode={mode}
        playerId={mode === "enter" || mode === "correct" ? acting!.id : undefined}
        version={score ? Number(score.version) : undefined}
        sides={sides}
        format={scoreFormat(game.scoreFormat)}
        stageStart={stage.startDate}
        today={game.today}
        initial={initial}
      />
    </>
  );
}
