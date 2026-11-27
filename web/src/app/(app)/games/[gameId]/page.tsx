import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { ResultStatusBadge } from "@/components/result-status-badge";
import { ScoreHistory } from "@/components/score-history";
import { ScoreTable } from "@/components/score-table";
import { TieMarkForm, markedOtherWay } from "@/components/tie-mark-form";
import { ButtonLink } from "@/components/ui/button";
import { ButtonStack } from "@/components/ui/button-stack";
import { ErrorState } from "@/components/ui/states";
import { Card } from "@/components/ui/card";
import { SubmitButton } from "@/components/ui/submit-button";
import { unvoidGame, voidGame } from "@/lib/actions";
import { apiWithSession } from "@/lib/api/server";
import { isId } from "@/lib/ids";
import { describeManagers, describeScoreFormat, formatDate } from "@/lib/league";
import { getViewer } from "@/lib/viewer";
import { PageColumn } from "@/components/shell/page-column";

export const metadata: Metadata = { title: "Match" };

/** What saving said, for the Match screen it lands on. */
const SAVED: Record<string, string> = {
  enter: "Score saved. Everyone in the league can see it now.",
  correct: "Correction saved: the score is as shown, and its history keeps every change.",
  override: "Override saved: the score is as shown, and its history keeps every change.",
  record: "Score recorded. The players in the game can still correct it while the Stage is open.",
  void: "Marked not played: the game counts for nothing.",
  unvoid: "No longer marked not played: the game can be scored again.",
};

/**
 * One League Game (#38, wireframe P3): both Sides, its Score set by set once
 * entered, who entered it and every change since; until then, the deadline
 * and, for a Player in it, the way to enter it. A Player in it corrects it
 * while the Stage is open; a Manager overrides it at any time; after the
 * close, its Players see it locked. A Manager settles one nobody scored by
 * recording it or marking it not played, and can undo that (#10). The League's Players and Managers see
 * it; anyone else gets a 404. In Foursomes (#14 part 3), a Player in it whose
 * score left the Division level for a place that moves is asked who goes.
 */
export default async function MatchPage({ params, searchParams }: PageProps<"/games/[gameId]">) {
  const [viewer, api, { gameId }, { saved, refused, tie }] = await Promise.all([
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
  const manager = game.canManage;
  const { score, stage } = game;
  const stageName = `Stage ${stage.number}`;
  const refusals: Record<string, string> = {
    "already-scored": "Someone entered this game's score first. Here it is.",
    "not-in-game": "Only the players in this game can enter its score.",
    "stage-closed": `${stageName} has closed, so its players can't change this game's score any more.`,
    "not-scored": "This game has no score to change yet.",
    "not-played": "A manager has marked this game as not played, so it takes no score.",
  };
  const savedMessage = typeof saved === "string" ? SAVED[saved] : undefined;

  // Foursomes (#14 part 3): the ties this Game's Division leaves its Players to settle, asked here as the score lands.
  const ladder =
    game.type === "Foursomes" && playing && !stage.closed
      ? (await api.GET("/leagues/{leagueId}/ladder", { params: { path: { leagueId: game.leagueId } } })).data
      : undefined;
  const division = ladder?.divisions.find((candidate) => Number(candidate.number) === Number(game.division));
  const divisionNames = Object.fromEntries(
    (division?.competitors ?? []).map((competitor) => [competitor.teamId, competitor.players.map((player) => player.name).join(" & ")]),
  );
  const refusal = typeof refused === "string" ? refusals[refused] : undefined;

  return (
    <PageColumn>
      <div className="flex flex-col gap-1">
        <h1 className="text-4xl leading-none font-black break-words">
          {sides[0]} v {sides[1]}
        </h1>
        <p className="text-fg-muted">
          {game.leagueName} · Division {game.division} · {stageName}
          {stage.closed ? " (closed)" : ""}
        </p>
        {/* Who runs its League (#220), by name: never an email. */}
        <p className="text-sm text-fg-muted">{describeManagers(game.managers)}</p>
      </div>

      {savedMessage && (
        <p role="status" className="rounded-md border border-success bg-success-muted px-4 py-3 text-sm">
          {savedMessage}
        </p>
      )}
      {refusal && (
        <p role="status" className="rounded-md border border-border bg-surface-muted px-4 py-3 text-sm">
          {refusal}
        </p>
      )}
      {tie === "changed" && (
        <ErrorState title="That didn't save" description="The tie changed meanwhile, or the Stage closed. This is how it stands now." />
      )}
      {tie === "pick" && <ErrorState title="Nothing saved" description="Pick who goes, then save." />}
      {tie === "invalid" && (
        <ErrorState title="Nothing saved" description="Pick one of the players level there, and not one already going the other way." />
      )}
      {acting &&
        division?.playersTies.map((playersTie) => (
          <TieMarkForm
            key={playersTie.place}
            leagueId={game.leagueId}
            playerId={acting.id}
            division={Number(division.number)}
            tie={playersTie}
            names={divisionNames}
            gameId={game.id}
            otherWay={markedOtherWay(playersTie, division.playersTies)}
          />
        ))}

      {score ? (
        <Card title="Score" aria-label="Score" aside={<ResultStatusBadge status={score.retired ? "retirement" : "played"} />}>
          <ScoreTable sides={sides} sets={score.sets} winner={Number(score.winner)} />
          <p className="mt-3 text-sm text-fg-muted">
            <strong className="text-fg">{sides[Number(score.winner) - 1]}</strong> won
            {score.retired ? " when the game stopped early" : ""} · played {formatDate(score.playedOn)} ·{" "}
            {score.recordedByManager
              ? `recorded by ${score.submittedBy ? `${score.submittedBy} (manager)` : "a manager"}`
              : `entered by ${score.submittedBy ?? "a player"}`}
          </p>
          <div className="mt-4 flex flex-col gap-2">
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
                <p role="note" className="rounded-md border border-border bg-surface-muted px-3 py-2 text-sm">
                  <strong>🔒 {stageName} is closed.</strong> This score can&apos;t be changed any more. If it&apos;s wrong, ask the
                  manager to override it.
                </p>
              )
            )}
          </div>
        </Card>
      ) : game.notPlayed ? (
        <Card title="Not played" aria-label="Not played" aside={<ResultStatusBadge status="void" />}>
          <div className="flex flex-col gap-3">
            <p className="text-sm">A manager marked this game as not played: it counts for nothing, for either side.</p>
            {manager && !stage.closed && (
              <form action={unvoidGame}>
                <input type="hidden" name="gameId" value={game.id} />
                <SubmitButton variant="secondary" fullWidth pendingLabel="Undoing…">
                  Undo: it can still be played
                </SubmitButton>
              </form>
            )}
          </div>
        </Card>
      ) : (
        <Card title="No score yet" aria-label="No score yet" aside={<ResultStatusBadge status="pending" />}>
          <ButtonStack>
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
            ) : manager && !stage.closed ? (
              <ButtonLink href={`/games/${game.id}/score`} variant="secondary" fullWidth>
                Record the score (manager)
              </ButtonLink>
            ) : (
              acting &&
              !playing && (
                <p className="text-sm text-fg-muted">{acting.name} doesn&apos;t play in this game, so can&apos;t enter its score.</p>
              )
            )}
            {manager && !stage.closed && (
              <form action={voidGame} className="flex flex-col gap-2">
                <input type="hidden" name="gameId" value={game.id} />
                <SubmitButton variant="secondary" fullWidth pendingLabel="Marking…">
                  Mark not played (manager)
                </SubmitButton>
                <p className="text-xs text-fg-muted">It then counts for nothing, for either side. You can undo it while the Stage is open.</p>
              </form>
            )}
          </ButtonStack>
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
    </PageColumn>
  );
}
