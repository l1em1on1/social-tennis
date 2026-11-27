import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ResultStatusBadge } from "@/components/result-status-badge";
import { HeldUpBanner } from "@/components/held-up-banner";
import { StandingsTable } from "@/components/standings-table";
import { Badge } from "@/components/ui/badge";
import { ButtonLink } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/states";
import { apiWithSession } from "@/lib/api/server";
import type { components } from "@/lib/api/schema";
import { cx } from "@/lib/cx";
import { isId } from "@/lib/ids";
import { describeManagers, formatDate, formatDates, gameLabel, labelledSide, sidesOf, typeLabel } from "@/lib/league";
import { formatSets } from "@/lib/score";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "Ladder" };

type Competitor = components["schemas"]["LadderCompetitor"];

const names = (competitor: Competitor) => competitor.players.map((player) => player.name).join(" & ");

/**
 * A League's ladder (#7, wireframe P5): every Division this Stage from the
 * top, each with its Standings and who would move if the Stage closed now
 * (#10), and every Game between its Teams (ADR-0031), with its Score once
 * entered and a link to its Match screen (#38). Past its deadline and still
 * open, it says what holds the Stage up (#10). The Acting
 * Player's Division is marked, with who they play, or it says they aren't on
 * the ladder. Only the League's Players and managers see it: anyone else gets
 * a 404. It names who runs it, and whoever does has a way to its Manager page (#220).
 * A Foursomes Division (#14) is 4 Players with 3 Games of rotating pairs,
 * ranked by the games each Player banked; a Player's own Games name their
 * partner first.
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
      <h1 className="text-4xl leading-none font-black break-words">{ladder.name}</h1>
      <p className="text-fg-muted">
        {ladder.clubName} · {typeLabel(ladder.type)}
      </p>
      {/* Who runs it (#220), by name: never an email. */}
      <p className="text-sm text-fg-muted">{describeManagers(ladder.managers)}</p>
      {ladder.canManage && (
        <div className="mt-1">
          <ButtonLink href={`/manage/leagues/${ladder.id}`} variant="secondary">
            Manage this league
          </ButtonLink>
        </div>
      )}
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

  const foursomes = ladder.type === "Foursomes";
  const me = viewer.actingPlayer?.id;
  // The API lets in a login with any of its Players in the League, and says which;
  // the Acting Player may be another of them (a parent switched to a child who isn't in it).
  const actingInLeague = viewer.actingPlayer ? ladder.yourPlayerIds.includes(viewer.actingPlayer.id) : false;
  const isMe = (competitor: Competitor) => competitor.players.some((player) => player.playerId === me);
  const mine = ladder.divisions.find((division) => division.competitors.some(isMe));
  const opponents = mine?.competitors.filter((competitor) => !isMe(competitor)).map(names) ?? [];
  const competitors = ladder.divisions.flatMap((division) => division.competitors);
  const teamName = (teamId: string) => {
    const team = competitors.find((competitor) => competitor.teamId === teamId);
    return team ? names(team) : "";
  };
  const list = opponents.length > 1 ? `${opponents.slice(0, -1).join(", ")} and ${opponents[opponents.length - 1]}` : opponents[0];
  const myTeams = competitors.filter(isMe).map((competitor) => competitor.teamId);

  return (
    <>
      {heading}
      <p className="text-sm">
        <strong>Stage {stage.number}</strong>{" "}
        <span className="text-fg-muted">
          {formatDates(stage.startDate, stage.endDate)}.{" "}
          {foursomes
            ? "Each Division plays 3 games, everyone partnering each of the others once, by "
            : "Everyone in a Division plays everyone else in it once, by "}
          {formatDate(stage.endDate)}.
        </span>
      </p>

      {ladder.ended && (
        <p role="note" className="rounded-md border border-border bg-surface-muted px-4 py-3 text-sm">
          The League has ended: this is how its last Stage finished.
        </p>
      )}
      {ladder.heldUp && (
        <HeldUpBanner
          stageNumber={Number(stage.number)}
          endDate={stage.endDate}
          unscored={ladder.divisions.flatMap((division) =>
            division.games
              .filter((game) => ladder.awaiting.unscoredGameIds.includes(game.id))
              .map((game) => ({ gameId: game.id, label: gameLabel(game, teamName) })),
          )}
          ties={ladder.awaiting.ties.map((tie) => ({
            division: Number(tie.division),
            teamIds: tie.teamIds,
            names: tie.teamIds.map(teamName),
          }))}
        />
      )}

      {mine ? (
        <p role="status" className="rounded-md border border-accent bg-accent-muted px-4 py-3 text-sm">
          You&apos;re in <strong>Division {mine.number}</strong>.{" "}
          {foursomes ? (
            <>
              You play 3 games with and against <strong>{list}</strong> by {formatDate(stage.endDate)}: arrange each game
              between the four of you.
            </>
          ) : (
            <>
              You play <strong>{list}</strong> by {formatDate(stage.endDate)}: arrange each game between you.
            </>
          )}
        </p>
      ) : actingInLeague ? (
        <p role="status" className="rounded-md border border-border bg-surface-muted px-4 py-3 text-sm">
          {viewer.actingPlayer?.name} isn&apos;t on the ladder this Stage. Ask the manager if that&apos;s a mistake.
        </p>
      ) : (
        // Not in this League: another of the login's Players is, or a Manager is looking.
        viewer.actingPlayer && (
          <p role="status" className="rounded-md border border-border bg-surface-muted px-4 py-3 text-sm">
            {viewer.actingPlayer.name} isn&apos;t in this league.{" "}
            <Link href="/leagues" className="font-medium text-accent underline">
              See {viewer.actingPlayer.name}&apos;s leagues
            </Link>
          </p>
        )
      )}

      {/* A column on a phone, as before; Divisions two by two from md (#225). */}
      <ol aria-label="Ladder" className="flex flex-col gap-3 md:grid md:grid-cols-2 md:items-start">
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
                <StandingsTable
                  number={Number(division.number)}
                  foursomes={foursomes ? { gameNotPlayed: division.games.some((game) => game.notPlayed) } : undefined}
                  standings={division.standings}
                  names={Object.fromEntries(division.competitors.map((competitor) => [competitor.teamId, names(competitor)]))}
                  mine={division.competitors.filter(isMe).map((competitor) => competitor.teamId)}
                />
                <h3 className="mt-3 text-sm font-semibold">
                  Games <span className="font-normal text-fg-muted">· {division.games.length}</span>
                </h3>
                <ul aria-label={`Division ${division.number} Games`} className="divide-y divide-border text-sm">
                  {division.games.map((game) => {
                    const ourGame = sidesOf(game).flat().some((teamId) => myTeams.includes(teamId));
                    const { score } = game;
                    return (
                      <li key={game.id}>
                        <Link
                          href={`/games/${game.id}`}
                          className="flex min-h-11 items-center justify-between gap-2 py-1.5 hover:underline"
                        >
                          <span className={cx("min-w-0 break-words", ourGame && "font-semibold")}>
                            {gameLabel(game, nameOf, myTeams)}
                          </span>
                          <span className="flex shrink-0 items-center gap-2">
                            {/* From the side named first: a Foursomes Player's own pair may be Side 2. */}
                            {score && <span className="font-mono text-xs">{formatSets(score.sets, labelledSide(game, myTeams))}</span>}
                            <ResultStatusBadge
                              status={score ? (score.retired ? "retirement" : "played") : game.notPlayed ? "void" : "pending"}
                            />
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
