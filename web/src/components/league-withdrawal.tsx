import { Badge } from "@/components/ui/badge";
import { Sheet } from "@/components/ui/sheet";
import { SubmitButton } from "@/components/ui/submit-button";
import { askToWithdraw, cancelWithdrawal } from "@/lib/actions";
import type { components } from "@/lib/api/schema";
import { formatDate } from "@/lib/league";

type PlayerLeague = components["schemas"]["PlayerLeague"];

const day = (instant: string | null | undefined) => (instant ? formatDate(instant.slice(0, 10)) : "");

/** Whether the Player's Team has left the League (#21): the card then says so in place of their ladder. */
export function hasWithdrawn(league: PlayerLeague) {
  return league.withdrawal?.status === "Accepted";
}

/**
 * Leaving a running League (#21), on the Player's card on Leagues: once the
 * Team has withdrawn, that it has; otherwise, for a Team on an open Stage's
 * ladder, a waiting request to take back, or Ask to leave behind a sheet
 * (with a Manager's earlier "no" above it). Before the start, or off the
 * ladder, a Player leaves by asking a Manager to take them out (#159); once
 * the League has ended, there's nothing left to leave.
 */
export function LeagueWithdrawal({ league, playerId }: { league: PlayerLeague; playerId: string }) {
  const { withdrawal, stage, place, partnerName, ended } = league;
  const who = partnerName ? `You and ${partnerName}` : "You";

  if (withdrawal?.status === "Accepted") {
    return (
      <p className="mt-2 text-sm">
        <Badge>Withdrawn</Badge>{" "}
        <span className="text-fg-muted">
          {who} left this league on {day(withdrawal.decidedAt)}.{" "}
          {stage && place && !ended
            ? `The place stays on the ladder until Stage ${stage.number} closes, but the games without a score won't be played: a manager marks them not played. Results already played stay.`
            : "Results already played stay."}
        </span>
      </p>
    );
  }
  if (!stage || !place || ended) return null;

  if (withdrawal?.status === "Pending") {
    return (
      <div className="mt-3 flex flex-col gap-2">
        <p className="text-sm">
          <Badge tone="warning">Asked to leave</Badge>{" "}
          <span className="text-fg-muted">Asked {day(withdrawal.requestedAt)}. A manager decides.</span>
        </p>
        <form action={cancelWithdrawal}>
          <input type="hidden" name="leagueId" value={league.id} />
          <input type="hidden" name="playerId" value={playerId} />
          <SubmitButton variant="secondary" pendingLabel="Taking back…" aria-label={`Take back the request to leave ${league.name}`}>
            Take back
          </SubmitButton>
        </form>
      </div>
    );
  }

  return (
    <div className="mt-3 flex flex-col gap-2">
      {withdrawal?.status === "Declined" && (
        <p className="text-sm">
          <Badge tone="danger">Not accepted</Badge>{" "}
          <span className="text-fg-muted">
            A manager kept you in the league on {day(withdrawal.decidedAt)}. Ask them if you think it&apos;s a mistake.
          </span>
        </p>
      )}
      <div>
        <Sheet title={`Leave ${league.name}?`} trigger="Ask to leave" triggerVariant="secondary">
          <p className="text-fg-muted">
            A manager decides. If they agree, {partnerName ? `you and ${partnerName} keep your` : "you keep your"} place on the
            ladder until Stage {stage.number} closes, then leave it. The games without a score by then won&apos;t be played: a
            manager marks them not played. Results already played stay.
          </p>
          {league.type === "Foursomes" && (
            <p className="text-fg-muted">At the close, the manager names a player to take your place, so the Division stays 4.</p>
          )}
          <form action={askToWithdraw}>
            <input type="hidden" name="leagueId" value={league.id} />
            <input type="hidden" name="playerId" value={playerId} />
            <SubmitButton fullWidth pendingLabel="Asking…" aria-label={`Ask to leave ${league.name}`}>
              Ask to leave
            </SubmitButton>
          </form>
        </Sheet>
      </div>
    </div>
  );
}
