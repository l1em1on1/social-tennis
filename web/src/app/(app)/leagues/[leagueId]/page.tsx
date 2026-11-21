import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { ButtonLink } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/states";
import { apiWithSession } from "@/lib/api/server";
import type { components } from "@/lib/api/schema";
import { cx } from "@/lib/cx";
import { isId } from "@/lib/ids";
import { formatDate, formatDates, typeLabel } from "@/lib/league";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "Ladder" };

type Competitor = components["schemas"]["LadderCompetitor"];

const names = (competitor: Competitor) => competitor.players.map((player) => player.name).join(" & ");

/**
 * A League's ladder (#7, wireframe P5): every Division this Stage from the
 * top, each with its Game, then the Bye. The Acting Player's Division is
 * marked, with who they play; or their Bye; or that they aren't on it. Only
 * the League's Players and Managers see it: anyone else gets a 404.
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
  const isMe = (competitor: Competitor) => competitor.players.some((player) => player.playerId === me);
  const mine = ladder.divisions.find((division) => division.competitors.some(isMe));
  const byeIsMine = ladder.bye !== null && isMe(ladder.bye);

  return (
    <>
      {heading}
      <p className="text-sm">
        <strong>Stage {stage.number}</strong>{" "}
        <span className="text-fg-muted">
          {formatDates(stage.startDate, stage.endDate)}. Play your game by {formatDate(stage.endDate)}.
        </span>
      </p>

      {mine ? (
        <p role="status" className="rounded-2xl border border-accent bg-accent-muted px-4 py-3 text-sm">
          You&apos;re in <strong>Division {mine.number}</strong>. You play{" "}
          <strong>{names(mine.competitors.find((competitor) => !isMe(competitor))!)}</strong> by {formatDate(stage.endDate)}:
          arrange the game between you.
        </p>
      ) : byeIsMine ? (
        <p role="status" className="rounded-2xl border border-border bg-surface-muted px-4 py-3 text-sm">
          You have the <strong>Bye</strong> this Stage: with an odd number, the last on the ladder sits out and doesn&apos;t move.
        </p>
      ) : (
        // A Player sees the ladder only for a League they're in; a Manager may be looking at anyone's.
        viewer.actingPlayer &&
        !viewer.roles.includes("Manager") && (
          <p role="status" className="rounded-2xl border border-border bg-surface-muted px-4 py-3 text-sm">
            {viewer.actingPlayer.name} isn&apos;t on the ladder this Stage. Ask the manager if that&apos;s a mistake.
          </p>
        )
      )}

      <ol aria-label="Ladder" className="flex flex-col gap-3">
        {ladder.divisions.map((division) => {
          const ours = division === mine;
          return (
            <li key={division.number}>
              <Card
                title={`Division ${division.number}`}
                aria-label={`Division ${division.number}`}
                aside={ours ? <Badge tone="accent">You</Badge> : <Badge tone="warning">To play</Badge>}
                className={cx(ours && "border-2 border-accent")}
              >
                <p className="flex flex-wrap items-baseline gap-x-2">
                  {division.competitors.map((competitor, index) => (
                    <span key={competitor.teamId} className="contents">
                      {index > 0 && <span className="text-sm text-fg-muted">{" v "}</span>}
                      <span className={cx("break-words", isMe(competitor) ? "font-bold" : "font-medium")}>{names(competitor)}</span>
                    </span>
                  ))}
                </p>
              </Card>
            </li>
          );
        })}
      </ol>
      {ladder.bye && (
        <Card title="Bye" aria-label="Bye" aside={byeIsMine ? <Badge tone="accent">You</Badge> : undefined}>
          <p className={cx("break-words", byeIsMine ? "font-bold" : "font-medium")}>{names(ladder.bye)}</p>
          <p className="text-sm text-fg-muted">Sits out Stage {stage.number} and doesn&apos;t move.</p>
        </Card>
      )}
      {back}
    </>
  );
}
