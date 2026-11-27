"use client";

import { useActionState, useState } from "react";
import { DivisionMark } from "@/components/division-mark";
import { Card } from "@/components/ui/card";
import { Field, Input } from "@/components/ui/field";
import { IconButton } from "@/components/ui/icon-button";
import { ErrorState } from "@/components/ui/states";
import { SubmitButton } from "@/components/ui/submit-button";
import { closeStage, type CloseStageState } from "@/lib/actions";
import { cx } from "@/lib/cx";
import { formatDate, relegate } from "@/lib/league";

export interface CloseTeam {
  teamId: string;
  name: string;
  rank: number;
  /** "2–1, +5", or "5 pts, 2–1, +5" where points rank (#214): its record, for the Manager to judge a tie by. */
  record: string;
  /** Withdrawn (#21): it leaves at the close, from the place it would have moved to, so nobody else moves for it. */
  withdrawn: boolean;
}

export interface CloseDivision {
  number: number;
  /** Its Teams in Standings order, ties in ladder order. */
  teams: CloseTeam[];
  /** Ties that decide who moves: Team ids, for the Manager to order. */
  ties: string[][];
}

export interface StageCloseFormProps {
  leagueId: string;
  stageNumber: number;
  stageEnd: string;
  leagueEnd: string;
  divisions: CloseDivision[];
  /** Teams off the ladder the Manager can add. */
  waiting: { teamId: string; name: string }[];
  /** The next Stage's proposed dates; none when this Stage is the League's last. */
  nextStage: { startDate: string; endDate: string } | null;
}

/** Where a Team goes next Stage, beside its arrow in words, with the Division's mark to save the row's space (#255). */
function destination(from: number, to: number) {
  if (to !== from) {
    return (
      <span className={cx("inline-flex items-center gap-1 font-semibold whitespace-nowrap", to < from ? "text-success" : "text-danger")}>
        {to < from ? "▲" : "▼"} to <DivisionMark division={to} />
      </span>
    );
  }
  return <span className="whitespace-nowrap text-fg-muted">stays</span>;
}

/**
 * Closing a Stage (#10, wireframe M6): each Division in its final order, with
 * ▲ ▼ to order the Teams of a tie that decides who moves, and where each
 * Team goes, as the close will do it (`relegate`), or that it leaves, where
 * it has withdrawn; the Teams waiting for a place to tick; and the next
 * Stage's dates. Confirming sends that order, and the API refuses it if the
 * Standings have changed since.
 */
export function StageCloseForm({ leagueId, stageNumber, stageEnd, leagueEnd, divisions, waiting, nextStage }: StageCloseFormProps) {
  const [state, action] = useActionState<CloseStageState, FormData>(closeStage, { errors: {} });
  const [orders, setOrders] = useState(() => divisions.map((division) => division.teams.map((team) => team.teamId)));
  const [joining, setJoining] = useState<string[]>([]);
  // Controlled, so a refusal keeps what the Manager typed: React resets a form's uncontrolled fields when its
  // action finishes, and the next close would otherwise use the proposal unseen (#217 review).
  const [startDate, setStartDate] = useState(nextStage?.startDate ?? "");
  const [endDate, setEndDate] = useState(nextStage?.endDate ?? "");
  const ends = nextStage === null;
  const next = ends ? null : relegate(orders, joining);
  const divisionOf = (teamId: string) => (next ? next.findIndex((division) => division.includes(teamId)) + 1 : 0);

  const swap = (division: number, at: number, by: -1 | 1) =>
    setOrders((current) =>
      current.map((order, index) => {
        if (index !== division) return order;
        const swapped = [...order];
        [swapped[at], swapped[at + by]] = [swapped[at + by], swapped[at]];
        return swapped;
      }),
    );

  return (
    <form action={action} className="flex flex-col gap-4">
      <input type="hidden" name="leagueId" value={leagueId} />
      {orders.map((order, index) => (
        <input key={index} type="hidden" name="division" value={order.join(",")} />
      ))}
      {joining.map((teamId) => (
        <input key={teamId} type="hidden" name="joining" value={teamId} />
      ))}
      {state.errors.form && <ErrorState title="The Stage didn't close" description={state.errors.form.join(" ")} />}
      {/* A withdrawn Team's place (#21): naming a replacement on this screen comes in part 3. */}
      {state.errors.replacements && <ErrorState title="The Stage didn't close" description={state.errors.replacements.join(" ")} />}

      {ends && (
        <p role="note" className="rounded-md border border-border bg-surface-muted px-4 py-3 text-sm">
          This is the League&apos;s last Stage: it ended on {formatDate(leagueEnd)}. Closing it ends the League, and nobody moves.
        </p>
      )}

      <ol aria-label="Divisions" className="flex flex-col gap-3">
        {divisions.map((division, d) => {
          const teams = new Map(division.teams.map((team) => [team.teamId, team]));
          const tieOf = (teamId: string) => division.ties.find((tie) => tie.includes(teamId));
          return (
            <li key={division.number}>
              <Card title={`Division ${division.number}`} aria-label={`Division ${division.number}`}>
                <ol aria-label={`Division ${division.number} order`} className="flex flex-col divide-y divide-border">
                  {orders[d].map((teamId, at) => {
                    const team = teams.get(teamId)!;
                    const tie = tieOf(teamId);
                    // ▲ ▼ only within a tie: the rest of the order is the Standings'.
                    const canUp = tie !== undefined && at > 0 && tie.includes(orders[d][at - 1]);
                    const canDown = tie !== undefined && at < orders[d].length - 1 && tie.includes(orders[d][at + 1]);
                    return (
                      <li key={teamId} className={cx("flex items-center justify-between gap-2 py-1.5 text-sm", tie && "bg-warning-muted -mx-2 px-2")}>
                        <span className="min-w-0 break-words">
                          <span className="text-fg-muted">{at + 1}.</span> <span className="font-medium">{team.name}</span>{" "}
                          <span className="text-fg-muted">· {team.record}</span>
                          {tie && <span className="sr-only">, tied</span>}
                        </span>
                        <span className="flex shrink-0 items-center gap-2">
                          {next &&
                            (team.withdrawn ? (
                              <span className="whitespace-nowrap text-fg-muted">leaves</span>
                            ) : (
                              destination(division.number, divisionOf(teamId))
                            ))}
                          {tie && (
                            <>
                              <IconButton icon="up" label={`Move ${team.name} up`} disabled={!canUp} onClick={() => swap(d, at, -1)} />
                              <IconButton icon="down" label={`Move ${team.name} down`} disabled={!canDown} onClick={() => swap(d, at, 1)} />
                            </>
                          )}
                        </span>
                      </li>
                    );
                  })}
                </ol>
                {division.ties.length > 0 && (
                  <p className="mt-2 text-xs text-fg-muted">
                    The highlighted Teams are tied, and the tie decides who moves: put them in the order you decide.
                  </p>
                )}
              </Card>
            </li>
          );
        })}
      </ol>

      {!ends && waiting.length > 0 && (
        <fieldset className="flex flex-col gap-2 rounded-md border border-border bg-surface p-4">
          <legend className="px-1 text-base font-semibold">Teams waiting for a place</legend>
          <p className="text-sm text-fg-muted">Ticked Teams start Stage {stageNumber + 1} at the bottom of Division {divisions.length}.</p>
          {waiting.map((team) => (
            <label key={team.teamId} className="flex min-h-11 items-center gap-3 text-sm">
              <input
                type="checkbox"
                className="size-5 accent-accent"
                checked={joining.includes(team.teamId)}
                onChange={(event) =>
                  setJoining((current) => (event.target.checked ? [...current, team.teamId] : current.filter((id) => id !== team.teamId)))
                }
              />
              {team.name}
            </label>
          ))}
        </fieldset>
      )}

      {nextStage && (
        <div className="flex flex-col gap-3 rounded-md border border-border bg-surface p-4">
          <h2 className="text-base font-semibold">Stage {stageNumber + 1}</h2>
          <Field
            id="nextStartDate"
            label="Starts"
            hint={`From the day after Stage ${stageNumber} ends (${formatDate(stageEnd)}) or later.`}
            error={state.errors.nextStartDate?.join(" ")}
          >
            {(control) => (
              <Input {...control} type="date" name="nextStartDate" value={startDate} onChange={(event) => setStartDate(event.target.value)} required />
            )}
          </Field>
          <Field id="nextEndDate" label="Ends" hint={`The League ends on ${formatDate(leagueEnd)}.`} error={state.errors.nextEndDate?.join(" ")}>
            {(control) => (
              <Input
                {...control}
                type="date"
                name="nextEndDate"
                value={endDate}
                onChange={(event) => setEndDate(event.target.value)}
                max={leagueEnd}
                required
              />
            )}
          </Field>
        </div>
      )}

      <SubmitButton fullWidth pendingLabel="Closing…">
        {ends ? `Close Stage ${stageNumber} and end the League` : `Close Stage ${stageNumber} and open Stage ${stageNumber + 1}`}
      </SubmitButton>
    </form>
  );
}
