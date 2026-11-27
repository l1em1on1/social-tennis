import { SubmitButton } from "@/components/ui/submit-button";
import type { components } from "@/lib/api/schema";
import { markTie } from "@/lib/actions";

type PlayersTie = components["schemas"]["LadderPlayersTie"];

const list = (names: string[]) => new Intl.ListFormat("en-GB").format(names);

/**
 * In a middle Division where all four are level, one group decides both who
 * goes up and who goes down: the Player marked for one can't be marked for
 * the other, so the form leaves them out.
 */
export function markedOtherWay(tie: PlayersTie, ties: PlayersTie[]): string | undefined {
  const same = (a: string[], b: string[]) => [...a].sort().join() === [...b].sort().join();
  return ties.find((other) => other.place !== tie.place && same(other.teamIds, tie.teamIds))?.mark?.teamId;
}

/**
 * A Foursomes tie the Division's Players settle themselves (#14 part 3; owner,
 * 2026-11-26): Players level on games at a place that moves, once every Game
 * has a score. Any of the four says who goes up, or down, however they decided
 * it; the last answer stands, and the Manager can still change it at the
 * close. Shown on the ladder and on the Game whose score made the tie.
 */
export function TieMarkForm({
  leagueId,
  playerId,
  division,
  tie,
  names,
  gameId,
  otherWay,
}: {
  leagueId: string;
  /** The Acting Player, in this Division. */
  playerId: string;
  division: number;
  tie: PlayersTie;
  /** Each Player's name, by their Team of one. */
  names: Record<string, string>;
  /** The Game it's said from, to come back to; the ladder otherwise. */
  gameId?: string;
  /** The Player already marked to go the other way from the same group (`markedOtherWay`): not offered. */
  otherWay?: string;
}) {
  const direction = tie.place === "Up" ? "up" : "down";
  const legend = `Who goes ${direction} from Division ${division}?`;
  return (
    <form action={markTie} className="flex flex-col gap-3 rounded-md border border-accent bg-accent-muted p-3 text-sm">
      <input type="hidden" name="leagueId" value={leagueId} />
      <input type="hidden" name="playerId" value={playerId} />
      <input type="hidden" name="division" value={division} />
      <input type="hidden" name="place" value={tie.place} />
      {gameId && <input type="hidden" name="gameId" value={gameId} />}
      <fieldset className="flex flex-col gap-2">
        <legend className="mb-1 font-semibold">{legend}</legend>
        <p className="text-fg-muted">
          {list(tie.teamIds.map((teamId) => names[teamId] ?? ""))} are level on games. Settle it between you, with a coin toss or
          however you like, and say who goes {direction}.
        </p>
        {tie.teamIds.filter((teamId) => teamId !== otherWay).map((teamId) => (
          <label key={teamId} className="flex min-h-11 items-center gap-3">
            <input
              type="radio"
              name="teamId"
              value={teamId}
              defaultChecked={tie.mark?.teamId === teamId}
              required
              className="size-5 accent-[var(--color-accent)]"
            />
            <span className="font-medium">{names[teamId] ?? ""}</span>
          </label>
        ))}
      </fieldset>
      {tie.mark && (
        <p>
          {tie.mark.markedBy ?? "A player"} said <strong>{names[tie.mark.teamId] ?? ""}</strong> goes {direction}. Any of you can
          change it until the Stage closes; the manager can too, at the close.
        </p>
      )}
      <div>
        <SubmitButton pendingLabel="Saving…">{tie.mark ? "Change" : "Save"}</SubmitButton>
      </div>
    </form>
  );
}

/** A Players' tie in words, for anyone not in that Division (#14 part 3). */
export function describePlayersTie(tie: PlayersTie, names: Record<string, string>): string {
  const direction = tie.place === "Up" ? "up" : "down";
  return tie.mark
    ? `${names[tie.mark.teamId] ?? ""} goes ${direction}: ${tie.mark.markedBy ?? "a player"} said so.`
    : `${list(tie.teamIds.map((teamId) => names[teamId] ?? ""))} are level on games: they'll say who goes ${direction}.`;
}
