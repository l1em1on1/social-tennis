import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { ButtonLink } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Field } from "@/components/ui/field";
import { Select } from "@/components/ui/select";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { SubmitButton } from "@/components/ui/submit-button";
import { addLeagueParticipant, approveLeagueJoinRequest, rejectLeagueJoinRequest } from "@/lib/actions";
import { apiWithSession } from "@/lib/api/server";
import { isId } from "@/lib/ids";
import { describeScoreFormat, describeStructure, formatDate, formatDates, typeLabel } from "@/lib/league";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "League" };

/**
 * One League as a Manager runs it (wireframe M3, without Teams: #6): Join
 * Requests to approve or reject first, since they hold up the start; then who
 * is in, by join time; then adding a Club member straight in.
 */
export default async function LeaguePage({ params, searchParams }: PageProps<"/manage/leagues/[leagueId]">) {
  const [viewer, api, { leagueId }, { created, error }] = await Promise.all([
    getViewer(),
    apiWithSession(),
    params,
    searchParams,
  ]);
  if (!viewer.roles.includes("Manager") || !isId(leagueId)) {
    notFound();
  }

  const { data: league, response } = await api.GET("/leagues/{leagueId}", { params: { path: { leagueId } } });
  if (response.status === 404) {
    notFound();
  }
  if (!league) {
    throw new Error(`GET /leagues/${leagueId} failed with ${response.status}`);
  }
  const { data: members } = await api.GET("/clubs/{clubId}/members", {
    params: { path: { clubId: league.clubId } },
  });
  if (!members) {
    throw new Error(`GET /clubs/${league.clubId}/members failed`);
  }

  const pending = league.joinRequests.filter((request) => request.status === "Pending");
  const decided = league.joinRequests.filter((request) => request.status !== "Pending");
  const inLeague = new Set(league.participants.map((participant) => participant.playerId));
  const addable = members.members.filter((member) => member.approved && !inLeague.has(member.playerId));

  return (
    <>
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-bold">{league.name}</h1>
        <p className="text-fg-muted">
          {league.clubName} · {typeLabel(league.type)}
        </p>
      </div>

      {created === "1" && (
        <p role="status" className="rounded-2xl border border-border bg-surface-muted px-4 py-3 text-sm">
          <strong>{league.name}</strong> is created. Players at the club can ask to join it now, or add them below.
        </p>
      )}
      {error === "conflict" && (
        <ErrorState
          title="That didn't go through"
          description="Someone else may have decided it first, or the player is no longer a member of the club. The lists below are up to date."
        />
      )}

      <Card title="Format">
        <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
          <dt className="text-fg-muted">Runs</dt>
          <dd>{formatDates(league.startDate, league.endDate)}</dd>
          <dt className="text-fg-muted">Structure</dt>
          <dd>{describeStructure(league)}</dd>
          <dt className="text-fg-muted">Score</dt>
          <dd>{describeScoreFormat(league.scoreFormat)}</dd>
        </dl>
      </Card>

      <Card title="Join requests" aside={pending.length > 0 && <Badge tone="warning">{`${pending.length} waiting`}</Badge>}>
        {pending.length === 0 ? (
          <p className="text-sm text-fg-muted">Nobody is waiting.</p>
        ) : (
          <ul className="divide-y divide-border">
            {pending.map((request) => (
              <li key={request.playerId} className="flex flex-col gap-2 py-3">
                <div>
                  <p className="font-medium">{request.name}</p>
                  <p className="text-sm text-fg-muted">
                    Rating {request.rating} · asked {formatDate(request.requestedAt.slice(0, 10))}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  <form action={approveLeagueJoinRequest}>
                    <input type="hidden" name="leagueId" value={league.id} />
                    <input type="hidden" name="playerId" value={request.playerId} />
                    <SubmitButton pendingLabel="Approving…" aria-label={`Approve ${request.name}`}>
                      Approve
                    </SubmitButton>
                  </form>
                  <form action={rejectLeagueJoinRequest}>
                    <input type="hidden" name="leagueId" value={league.id} />
                    <input type="hidden" name="playerId" value={request.playerId} />
                    <SubmitButton variant="secondary" pendingLabel="Rejecting…" aria-label={`Reject ${request.name}`}>
                      Reject
                    </SubmitButton>
                  </form>
                </div>
              </li>
            ))}
          </ul>
        )}
        {decided.some((request) => request.status === "Rejected") && (
          <div className="mt-3 flex flex-col gap-1">
            <p className="text-sm font-medium">Rejected</p>
            <ul aria-label="Rejected requests" className="text-sm text-fg-muted">
              {decided
                .filter((request) => request.status === "Rejected")
                .map((request) => (
                  <li key={request.playerId}>{request.name}</li>
                ))}
            </ul>
          </div>
        )}
      </Card>

      <Card title={`Players (${league.participants.length})`}>
        {league.participants.length === 0 ? (
          <EmptyState
            title="No players yet"
            description="Approve requests above, or add club members below. They don't need to have signed in."
          />
        ) : (
          <ul aria-label="Players in the league" className="divide-y divide-border">
            {league.participants.map((participant) => (
              <li key={participant.playerId} className="flex min-h-12 items-center justify-between gap-3 py-2">
                <div>
                  <p className="font-medium">{participant.name}</p>
                  <p className="text-sm text-fg-muted">Joined {formatDate(participant.joinedAt.slice(0, 10))}</p>
                </div>
                <span className="font-mono text-sm">{participant.rating}</span>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card title="Add a club member">
        {addable.length === 0 ? (
          <p className="text-sm text-fg-muted">
            Every member of {league.clubName} is in this league. Add new players to the club on Manage first.
          </p>
        ) : (
          <form action={addLeagueParticipant} className="flex flex-col gap-3">
            <input type="hidden" name="leagueId" value={league.id} />
            <Field id="add-participant" label="Player" hint="Added straight in, with no request to approve.">
              {(control) => (
                <Select {...control} name="playerId" required defaultValue="">
                  <option value="" disabled>
                    Pick a player…
                  </option>
                  {addable.map((member) => (
                    <option key={member.playerId} value={member.playerId}>
                      {member.name} ({member.email})
                    </option>
                  ))}
                </Select>
              )}
            </Field>
            <SubmitButton variant="secondary" pendingLabel="Adding…">
              Add to league
            </SubmitButton>
          </form>
        )}
      </Card>

      <ButtonLink href="/manage" variant="ghost">
        Back to Manage
      </ButtonLink>
    </>
  );
}
