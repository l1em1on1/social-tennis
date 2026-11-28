import { Badge } from "@/components/ui/badge";
import { ButtonRow } from "@/components/ui/button-stack";
import { Card } from "@/components/ui/card";
import { Sheet } from "@/components/ui/sheet";
import { SubmitButton } from "@/components/ui/submit-button";
import { decideWithdrawal } from "@/lib/actions";
import { formatDate } from "@/lib/league";

export interface WithdrawalRequestRow {
  id: string;
  /** The Player who asked. */
  playerName: string;
  /** Their Team as the ladder names it: in doubles, both Players. */
  teamName: string;
  requestedAt: string;
}

/**
 * What withdrawing does (#21), as the Manager confirms it: said the same way
 * when they accept a request and when they withdraw a Team themselves.
 */
export function WithdrawalConsequences({ teamName, stageNumber, foursomes }: { teamName: string; stageNumber: number; foursomes: boolean }) {
  return (
    <>
      <p className="text-fg-muted">
        {teamName} keeps its place until Stage {stageNumber} closes, so the Division keeps its shape. Its games without a score go
        to your to-do, to mark not played. Results already played stay.
      </p>
      <p className="text-fg-muted">
        {foursomes
          ? "When you close the Stage, you name a player to take the place, so the Division stays 4."
          : "When you close the Stage, it leaves the ladder, or you name a player waiting for a place to take it."}
      </p>
    </>
  );
}

/**
 * Requests to leave the League (#21), on its Manager page: who asked, for
 * which Team, and when, with Accept behind a sheet that says what follows,
 * and Decline, which changes nothing. Shown only while one is waiting.
 */
export function WithdrawalRequests({
  leagueId,
  stageNumber,
  foursomes,
  requests,
}: {
  leagueId: string;
  stageNumber: number;
  foursomes: boolean;
  requests: WithdrawalRequestRow[];
}) {
  if (requests.length === 0) return null;
  return (
    <Card title="Asking to leave" aside={<Badge tone="warning">{`${requests.length} waiting`}</Badge>}>
      <ul aria-label="Requests to leave" className="divide-y divide-border">
        {requests.map((request) => (
          <li key={request.id} className="flex flex-col gap-2 py-3">
            <div className="min-w-0">
              <p className="font-medium break-words">{request.playerName}</p>
              <p className="text-sm text-fg-muted">
                {request.teamName !== request.playerName && <>For {request.teamName}. </>}
                Asked {formatDate(request.requestedAt.slice(0, 10))}
              </p>
            </div>
            <ButtonRow>
              <Sheet
                title={`${request.teamName} leaving?`}
                trigger={
                  <>
                    Accept<span className="sr-only"> {request.teamName} leaving</span>
                  </>
                }
                triggerVariant="primary"
              >
                <WithdrawalConsequences teamName={request.teamName} stageNumber={stageNumber} foursomes={foursomes} />
                <form action={decideWithdrawal}>
                  <input type="hidden" name="leagueId" value={leagueId} />
                  <input type="hidden" name="requestId" value={request.id} />
                  <input type="hidden" name="accept" value="true" />
                  <SubmitButton fullWidth pendingLabel="Accepting…" aria-label={`Accept: ${request.teamName} leaves the league`}>
                    Accept: {request.teamName} leaves
                  </SubmitButton>
                </form>
              </Sheet>
              <form action={decideWithdrawal}>
                <input type="hidden" name="leagueId" value={leagueId} />
                <input type="hidden" name="requestId" value={request.id} />
                <input type="hidden" name="accept" value="false" />
                <SubmitButton variant="secondary" pendingLabel="Declining…" aria-label={`Decline ${request.playerName}'s request to leave`}>
                  Decline
                </SubmitButton>
              </form>
            </ButtonRow>
          </li>
        ))}
      </ul>
    </Card>
  );
}
