import Link from "next/link";
import { ResultStatusBadge } from "@/components/result-status-badge";
import { Badge } from "@/components/ui/badge";
import { ButtonLink } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import type { components } from "@/lib/api/schema";
import { cx } from "@/lib/cx";
import { formatDate } from "@/lib/league";
import { formatSet } from "@/lib/score";

type League = components["schemas"]["PlayerGamesLeague"];
type Game = components["schemas"]["PlayerGame"];

/** A Score from the Player's side: their games first, and whether they won. */
function fromYourSide(game: Game) {
  const score = game.score;
  if (!score) return null;
  const mine = Number(game.yourSide) === 1;
  const sets = score.sets.map((set) =>
    formatSet(
      mine
        ? set
        : { ...set, first: set.second, second: set.first, firstTiebreak: set.secondTiebreak, secondTiebreak: set.firstTiebreak },
    ),
  );
  return { line: sets.join(" "), won: Number(score.winner) === Number(game.yourSide), retired: score.retired };
}

/** The Games still owing a Score: none, and not marked not played. */
export const owedGames = (league: League) => league.games.filter((game) => !game.score && !game.notPlayed);

/**
 * A League's Stage past its deadline and still open (#10), above every card
 * on Home (wireframe P2, "held up"): what holds it up, counted; the ladder
 * names each. Not a live region: it says how things stand when the page loads.
 */
export function HeldUpNotice({ league }: { league: League }) {
  const { stage } = league;
  const unscored = Number(league.unscoredGames);
  const ties = Number(league.openTies);
  return (
    <section
      aria-label={`${league.leagueName}: Stage held up`}
      className="flex flex-col gap-1 rounded-md border border-warning bg-warning-muted px-4 py-3 text-sm"
    >
      <p className="font-semibold">
        {league.leagueName}: Stage {Number(stage.number)} ended on {formatDate(stage.endDate)} and is held up
      </p>
      <p>
        {unscored > 0 || ties > 0
          ? `${[
              unscored > 0 && `${unscored} ${unscored === 1 ? "game has" : "games have"} no score`,
              ties > 0 && `${ties} ${ties === 1 ? "tie decides" : "ties decide"} who moves`,
            ]
              .filter(Boolean)
              .join(", and ")
              .replace(/^./, (first) => first.toUpperCase())}. Your manager settles them; scores stay editable until it closes.`
          : "Everything is settled: your manager closes it next."}
      </p>
      <Link href={`/leagues/${league.leagueId}`} className="inline-flex min-h-11 items-center font-medium text-accent underline">
        See what&apos;s waiting
      </Link>
    </section>
  );
}

/**
 * One League on Home (#42, wireframe P2): the Player's Division and Games
 * this Stage, each opening its Match screen, and the deadline. A card with
 * Games owing a Score is the loudest thing on the page: an amber edge, the
 * "Score needed" badge, and a primary button to enter it.
 */
export function PlayerLeagueGames({ league }: { league: League }) {
  const { stage } = league;
  const division = league.division === null || league.division === undefined ? null : Number(league.division);
  const owed = owedGames(league);
  return (
    <Card
      title={league.leagueName}
      aria-label={league.leagueName}
      aside={division ? <Badge tone="accent">Division {division}</Badge> : <Badge>Not on the ladder</Badge>}
      className={cx(owed.length > 0 && "border-l-4 border-warning")}
    >
      <div className="flex flex-col gap-3">
        {division === null ? (
          <p className="text-sm text-fg-muted">You&apos;re not on the ladder this Stage. Ask the manager if that&apos;s a mistake.</p>
        ) : (
          <ul aria-label={`Your games in ${league.leagueName}`} className="divide-y divide-border text-sm">
            {league.games.map((game) => {
              const score = fromYourSide(game);
              return (
                <li key={game.gameId}>
                  <Link href={`/games/${game.gameId}`} className="flex min-h-11 items-center justify-between gap-2 py-2 hover:underline">
                    <span className="min-w-0 break-words">
                      {game.partners.length > 0 && <>With {game.partners.join(" & ")} </>}v <strong>{game.opponents.join(" & ")}</strong>
                      {score && (
                        <span className="block text-xs text-fg-muted">
                          {score.won ? "Won" : "Lost"} <span className="font-mono">{score.line}</span>
                          {score.retired ? " (retired)" : ""}
                        </span>
                      )}
                    </span>
                    <ResultStatusBadge
                      status={game.score ? (game.score.retired ? "retirement" : "played") : game.notPlayed ? "void" : "pending"}
                    />
                  </Link>
                </li>
              );
            })}
          </ul>
        )}
        <p className="text-sm text-fg-muted">
          Stage {Number(stage.number)} · play by <strong className="text-fg">{formatDate(stage.endDate)}</strong>
        </p>
        <div className="flex gap-2">
          {/* One Game to score goes straight to its entry; with more, its Match screen, as the wireframe's doubles card does. */}
          {owed.length === 1 && (
            <ButtonLink href={`/games/${owed[0].gameId}/score`} className="flex-1">
              Enter score
            </ButtonLink>
          )}
          {owed.length > 1 && (
            <ButtonLink href={`/games/${owed[0].gameId}`} className="flex-1">
              Enter a score
            </ButtonLink>
          )}
          <ButtonLink href={`/leagues/${league.leagueId}`} variant="secondary" className={cx(owed.length === 0 && "flex-1")}>
            Ladder
          </ButtonLink>
        </div>
      </div>
    </Card>
  );
}
