import type { Metadata } from "next";
import { notFound, redirect } from "next/navigation";
import { ScoreEntryForm } from "@/components/score-entry-form";
import { apiWithSession } from "@/lib/api/server";
import { isId } from "@/lib/ids";
import { describeScoreFormat } from "@/lib/league";
import { scoreFormat } from "@/lib/score";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "Enter score" };

/**
 * Entering a Game's Score (#38, wireframe P4), as the Acting Player, who must
 * play in it. A Game that has a Score, or an Acting Player who isn't in it,
 * goes back to the Match screen, which says so.
 */
export default async function EnterScorePage({ params }: PageProps<"/games/[gameId]/score">) {
  const [viewer, api, { gameId }] = await Promise.all([getViewer(), apiWithSession(), params]);
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
  if (game.score) {
    redirect(`/games/${gameId}?refused=already-scored`);
  }
  if (!acting || !game.yourPlayerIds.includes(acting.id)) {
    redirect(`/games/${gameId}?refused=not-in-game`);
  }

  const sides = game.sides.map((side) => side.players.map((player) => player.name).join(" & ")) as [string, string];
  return (
    <>
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-bold">Enter score</h1>
        <p className="text-fg-muted break-words">
          {sides[0]} v {sides[1]} · {describeScoreFormat(game.scoreFormat)}
        </p>
        {viewer.players.length > 1 && <p className="text-sm text-fg-muted">You&apos;re entering it as {acting.name}.</p>}
      </div>
      <ScoreEntryForm
        gameId={game.id}
        playerId={acting.id}
        sides={sides}
        format={scoreFormat(game.scoreFormat)}
        stageStart={game.stage.startDate}
        today={game.today}
      />
    </>
  );
}
