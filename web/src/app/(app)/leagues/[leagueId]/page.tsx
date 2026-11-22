import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ResultStatusBadge } from "@/components/result-status-badge";
import { Badge } from "@/components/ui/badge";
import { ButtonLink } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/states";
import { apiWithSession } from "@/lib/api/server";
import type { components } from "@/lib/api/schema";
import { cx } from "@/lib/cx";
import { isId } from "@/lib/ids";
import { formatDate, formatDates, typeLabel } from "@/lib/league";
import { formatSets } from "@/lib/score";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "Ladder" };

type Competitor = components["schemas"]["LadderCompetitor"];

const names = (competitor: Competitor) => competitor.players.map((player) => player.name).join(" & ");

/**
 * A League's ladder (#7, wireframe P5): every Division this Stage from the
 * top, each with its Teams and every Game between them (ADR-0031), with its
 * Score once entered and a link to its Match screen (#38). The Acting
 * Player's Division is marked, with who they play, or it says they aren't on
 * the ladder. Only the League's Players and Managers see it: anyone else gets
 * a 404.
 */
export default async function LadderPage({ params }: PageProps<"/leagues/[leagueId]">) {
  const [viewer, api, { leagueId }] = await Promise.all([getViewer(), apiWithSession(), params]);
  if (!isId(leagueId)) {
    notFound();
  }

  const { data: ladder, response } = await api.GET("/leagues/{leagueId}/ladder", { params: { path: { leagueId } } });
  if (response.status === 404) {
    notFound();
  }
  if (!ladder) {
    throw new Error(`GET /leagues/${leagueId}/ladder failed with ${response.status}`);
  }

  const back = (
    <div>
      <ButtonLink href="/leagues" variant="secondary">
        All leagues
      </ButtonLink>
    </div>
  );
  const heading = (
    <div className="flex flex-col gap-1">
      <h1 className="text-2xl font-bold break-words">{ladder.name}</h1>
      <p className="text-fg-muted">
        {ladder.clubName} · {typeLabel(ladder.type)}
      </p>
    </div>
  );

  const { stage } = ladder;
  if (!stage) {
    return (
      <>
        {heading}
        <EmptyState
          title="This league hasn't started"
          description="The manager is still adding players. The ladder appears when they start the league."
        />
        {back}
      </>
    );
  }

  const me = viewer.actingPlayer?.id;
  // The API lets in a login with any of its Players in the League, and says which;
  // the Acting Player may be another of them (a parent switched to a child who isn't in it).
  const actingInLeague = viewer.actingPlayer ? ladder.yourPlayerIds.includes(viewer.actingPlayer.id) : false;
  const isMe = (competitor: Competitor) => competitor.players.some((player) => player.playerId === me);
  const mine = ladder.divisions.find((division) => division.competitors.some(isMe));
  const opponents = mine?.competitors.filter((competitor) => !isMe(competitor)).map(names) ?? [];
  const list = opponents.length > 1 ? `${opponents.slice(0, -1).join(", ")} and ${opponents[opponents.length - 1]}` : opponents[0];

  return (
    <>
      {heading}
      <p className="text-sm">
        <strong>Stage {stage.number}</strong>{" "}
        <span className="text-fg-muted">
          {formatDates(stage.startDate, stage.endDate)}. Everyone in a Division plays everyone else in it once, by{" "}
          {formatDate(stage.endDate)}.
        </span>
      </p>

      {mine ? (
        <p role="status" className="rounded-2xl border border-accent bg-accent-muted px-4 py-3 text-sm">
          You&apos;re in <strong>Division {mine.number}</strong>. You play <strong>{list}</strong> by {formatDate(stage.endDate)}:
          arrange each game between you.
        </p>
      ) : actingInLeague ? (
        <p role="status" className="rounded-2xl border border-border bg-surface-muted px-4 py-3 text-sm">
          {viewer.actingPlayer?.name} isn&apos;t on the ladder this Stage. Ask the manager if that&apos;s a mistake.
        </p>
      ) : (
        // Not in this League: another of the login's Players is, or a Manager is looking.
        viewer.actingPlayer && (
          <p role="status" className="rounded-2xl border border-border bg-surface-muted px-4 py-3 text-sm">
            {viewer.actingPlayer.name} isn&apos;t in this league.{" "}
            <Link href="/leagues" className="font-medium text-accent underline">
              See {viewer.actingPlayer.name}&apos;s leagues
            </Link>
          </p>
        )
      )}

      <ol aria-label="Ladder" className="flex flex-col gap-3">
        {ladder.divisions.map((division) => {
          const ours = division === mine;
          const nameOf = (teamId: string) => {
            const team = division.competitors.find((competitor) => competitor.teamId === teamId);
            return team ? names(team) : "";
          };
          return (
            <li key={division.number}>
              <Card
                title={`Division ${division.number}`}
                aria-label={`Division ${division.number}`}
                aside={ours ? <Badge tone="accent">You</Badge> : undefined}
                className={cx(ours && "border-2 border-accent")}
              >
                <ol aria-label={`Division ${division.number} Teams`} className="flex flex-col gap-1">
                  {division.competitors.map((competitor) => (
                    <li key={competitor.teamId} className={cx("break-words", isMe(competitor) ? "font-bold" : "font-medium")}>
                      <span className="text-fg-muted">{competitor.position}.</span> {names(competitor)}
                    </li>
                  ))}
                </ol>
                <h3 className="mt-3 text-sm font-semibold">
                  Games <span className="font-normal text-fg-muted">· {division.games.length}</span>
                </h3>
                <ul aria-label={`Division ${division.number} Games`} className="divide-y divide-border text-sm">
                  {division.games.map((game) => {
                    const ourGame = [game.firstTeamId, game.secondTeamId].some((teamId) =>
                      division.competitors.some((competitor) => competitor.teamId === teamId && isMe(competitor)),
                    );
                    const { score } = game;
                    return (
                      <li key={game.id}>
                        <Link
                          href={`/games/${game.id}`}
                          className="flex items-center justify-between gap-2 py-1.5 hover:underline"
                        >
                          <span className={cx("min-w-0 break-words", ourGame && "font-semibold")}>
                            {nameOf(game.firstTeamId)} v {nameOf(game.secondTeamId)}
                          </span>
                          <span className="flex shrink-0 items-center gap-2">
                            {score && <span className="font-mono text-xs">{formatSets(score.sets)}</span>}
                            <ResultStatusBadge status={score ? (score.retired ? "retirement" : "played") : "pending"} />
                          </span>
                        </Link>
                      </li>
                    );
                  })}
                </ul>
              </Card>
            </li>
          );
        })}
      </ol>
      {back}
    </>
  );
}
