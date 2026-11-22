import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { TeamPairingForm } from "@/components/team-pairing-form";
import { Badge } from "@/components/ui/badge";
import { ButtonLink } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Field, Input } from "@/components/ui/field";
import { Select } from "@/components/ui/select";
import { Sheet } from "@/components/ui/sheet";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { SubmitButton } from "@/components/ui/submit-button";
import {
  addLeagueParticipant,
  approveLeagueJoinRequest,
  rejectLeagueJoinRequest,
  removeLeagueParticipant,
  renameLeague,
  unpairTeam,
} from "@/lib/actions";
import { apiWithSession } from "@/lib/api/server";
import { isId } from "@/lib/ids";
import { describeScoreFormat, describeStructure, formatDate, formatDates, typeLabel } from "@/lib/league";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "League" };

/** Matches the API's limit (League.NameMaxLength). */
const NAME_MAX_LENGTH = 200;

/**
 * One League as a Manager runs it (wireframe M3): its name, which the Manager
 * can change (#153); starting it, or its current Stage once started (#7);
 * Join Requests to approve or reject; then who is in, by join time, with each
 * one's Division once started; in a doubles League, its Teams and the Players
 * without a partner, to pair (#6); then adding a Club member straight in.
 */
export default async function LeaguePage({ params, searchParams }: PageProps<"/manage/leagues/[leagueId]">) {
  const [viewer, api, { leagueId }, { created, error, started: justStarted }] = await Promise.all([
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
  // Removal keeps a rejected request, and the API refuses a second ask (409), so
  // a Player who was turned down can only be added back.
  const turnedDown = new Set(decided.filter((request) => request.status === "Rejected").map((request) => request.playerId));
  const addable = members.members.filter((member) => member.approved && !inLeague.has(member.playerId));
  // Doubles: the Manager pairs Players into Teams (#6). Singles Teams of one,
  // and Foursomes' lack of Teams, need nothing on this page.
  const doubles = ["DoublesMen", "DoublesWomen", "DoublesMix"].includes(league.type);
  const nameOf = (playerId: string) => league.participants.find((p) => p.playerId === playerId)?.name ?? "";
  const unpaired = league.participants.filter((participant) => participant.teamId === null);
  // A pair asking together (#6): who with, and in mixed doubles a warning, not
  // a refusal, when both are the same gender (owner, 2026-11-20).
  const withPartner = (request: (typeof league.joinRequests)[number]) =>
    request.partner ? `${request.name} with ${request.partner.name}` : request.name;
  const sameGender = (request: (typeof league.joinRequests)[number]) =>
    league.type === "DoublesMix" && request.gender !== null && request.gender === request.partner?.gender;
  // Starting (#7): only a Divisional League of the Team-based Types, once.
  // Afterwards a Team on the ladder is fixed: no Unpair, and no Remove for its
  // Players (a Withdrawal, #21). Anyone not on it can still come and go.
  const stage = league.stage;
  const canStart = !stage && league.structure === "Divisional" && league.type !== "Foursomes";
  const teamOf = (teamId: string | null) => league.teams.find((team) => team.id === teamId);
  const onLadder = (teamId: string | null) => teamOf(teamId)?.position != null;
  const placeBadge = (teamId: string | null) => {
    if (!stage) return null;
    const team = teamOf(teamId);
    if (team?.position == null) return <Badge>Not on the ladder</Badge>;
    return <Badge tone="accent">{`Division ${team.division}`}</Badge>;
  };

  return (
    <>
      {/* Keyed by the name: a rename remounts it, so the sheet starts closed with the new name. */}
      <div key={league.name} className="flex items-start justify-between gap-3">
        <div className="flex min-w-0 flex-col gap-1">
          <h1 className="text-2xl font-bold break-words">{league.name}</h1>
          <p className="text-fg-muted">
            {league.clubName} · {typeLabel(league.type)}
          </p>
        </div>
        <Sheet
          title={`Rename ${league.name}`}
          trigger={
            <>
              Rename<span className="sr-only"> {league.name}</span>
            </>
          }
        >
          <form action={renameLeague} className="flex flex-col gap-4">
            <input type="hidden" name="leagueId" value={league.id} />
            <Field id="league-name" label="League name" hint="Only the name changes: players, requests and format stay.">
              {(control) => (
                <Input
                  {...control}
                  name="name"
                  required
                  maxLength={NAME_MAX_LENGTH}
                  autoComplete="off"
                  defaultValue={league.name}
                />
              )}
            </Field>
            <SubmitButton fullWidth pendingLabel="Saving…">
              Save name
            </SubmitButton>
          </form>
        </Sheet>
      </div>

      {stage ? (
        <p className="text-sm">
          <Badge tone="success">Started</Badge>{" "}
          <span className="text-fg-muted">
            Stage {stage.number} runs {formatDates(stage.startDate, stage.endDate)}.
          </span>
        </p>
      ) : (
        canStart && (
          <div>
            <ButtonLink href={`/manage/leagues/${league.id}/start`}>Start the league</ButtonLink>
          </div>
        )
      )}
      {justStarted === "1" && stage && (
        <p role="status" className="rounded-2xl border border-border bg-surface-muted px-4 py-3 text-sm">
          <strong>{league.name}</strong> has started. Each Division&apos;s Game is waiting to be played by{" "}
          {formatDate(stage.endDate)}.
        </p>
      )}

      {error === "name" && (
        <ErrorState title="That league name didn't work" description="Enter a name of up to 200 characters." />
      )}

      {created === "1" && (
        <p role="status" className="rounded-2xl border border-border bg-surface-muted px-4 py-3 text-sm">
          <strong>{league.name}</strong> is created. Players at the club can ask to join it now, or add them below.
        </p>
      )}
      {error === "pair" && (
        <ErrorState title="That pairing didn't work" description="Pick two different players without a partner." />
      )}
      {error === "partner-paired" && (
        <ErrorState
          title="That pair can't be approved"
          description="One of them already has a partner in this league. Unpair them first. Or the player who asked can take the request back and ask with someone else; rejecting it stops them asking again, though you can still add them."
        />
      )}
      {error === "partner-gone" && (
        <ErrorState
          title="That pair can't be approved"
          description="The partner they named is no longer a member of the club. The player who asked can take the request back and ask with someone else; rejecting it stops them asking again, though you can still add them."
        />
      )}
      {error === "on-the-ladder" && (
        <ErrorState
          title="They're on the ladder"
          description="Once the league has started, a team on its ladder stays together and in the league. Leaving it is a withdrawal, which isn't built yet."
        />
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
                  <p className="font-medium">{withPartner(request)}</p>
                  <p className="text-sm text-fg-muted">
                    {request.partner ? "Asked together" : "Asked"} {formatDate(request.requestedAt.slice(0, 10))}
                  </p>
                  {sameGender(request) && (
                    <p className="text-sm text-fg-muted">
                      Both are the same gender. A mixed doubles team is usually a man and a woman, but you can still approve them.
                    </p>
                  )}
                </div>
                <div className="flex flex-wrap gap-2">
                  <form action={approveLeagueJoinRequest}>
                    <input type="hidden" name="leagueId" value={league.id} />
                    <input type="hidden" name="playerId" value={request.playerId} />
                    <SubmitButton pendingLabel="Approving…" aria-label={`Approve ${withPartner(request)}`}>
                      Approve
                    </SubmitButton>
                  </form>
                  <form action={rejectLeagueJoinRequest}>
                    <input type="hidden" name="leagueId" value={league.id} />
                    <input type="hidden" name="playerId" value={request.playerId} />
                    <SubmitButton variant="secondary" pendingLabel="Rejecting…" aria-label={`Reject ${withPartner(request)}`}>
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
                  <li key={request.playerId}>{withPartner(request)}</li>
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
                  <p className="text-sm text-fg-muted">
                    Joined {formatDate(participant.joinedAt.slice(0, 10))} {placeBadge(participant.teamId)}
                  </p>
                </div>
                <div className="flex shrink-0 items-center gap-3">
                  {/* Behind a sheet: removing is one tap away from a name in a list. On the ladder, leaving is a Withdrawal (#21). */}
                  {!onLadder(participant.teamId) && (
                  <Sheet
                    title={`Remove ${participant.name}?`}
                    trigger={
                      <>
                        Remove<span className="sr-only"> {participant.name}</span>
                      </>
                    }
                  >
                    <p className="text-fg-muted">
                      {participant.name} will no longer be in {league.name}.{" "}
                      {turnedDown.has(participant.playerId)
                        ? "Their request to join was turned down, so they can't ask again, but you can add them back."
                        : "They can ask to join again, or you can add them back."}
                    </p>
                    <form action={removeLeagueParticipant}>
                      <input type="hidden" name="leagueId" value={league.id} />
                      <input type="hidden" name="playerId" value={participant.playerId} />
                      <SubmitButton variant="danger" fullWidth pendingLabel="Removing…">
                        Remove from league
                      </SubmitButton>
                    </form>
                  </Sheet>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </Card>

      {doubles && (
        <Card title={`Teams (${league.teams.length})`}>
          {league.teams.length === 0 ? (
            <p className="text-sm text-fg-muted">No teams yet. Pair the players below.</p>
          ) : (
            <ul aria-label="Teams in the league" className="divide-y divide-border">
              {league.teams.map((team) => {
                const names = team.playerIds.map(nameOf).join(" & ");
                return (
                  <li key={team.id} className="flex min-h-12 items-center justify-between gap-3 py-2">
                    <p className="font-medium">
                      {names} {placeBadge(team.id)}
                    </p>
                    {team.position == null && (
                      <form action={unpairTeam}>
                        <input type="hidden" name="leagueId" value={league.id} />
                        <input type="hidden" name="teamId" value={team.id} />
                        <SubmitButton variant="secondary" pendingLabel="Unpairing…" aria-label={`Unpair ${names}`}>
                          Unpair
                        </SubmitButton>
                      </form>
                    )}
                  </li>
                );
              })}
            </ul>
          )}
          <div className="mt-4 flex flex-col gap-3">
            <h3 className="font-semibold">Without a partner ({unpaired.length})</h3>
            {unpaired.length === 0 ? (
              <p className="text-sm text-fg-muted">Everyone in the league has a partner.</p>
            ) : (
              <ul aria-label="Players without a partner" className="text-sm">
                {unpaired.map((participant) => (
                  <li key={participant.playerId}>{participant.name}</li>
                ))}
              </ul>
            )}
            {unpaired.length >= 2 && (
              <TeamPairingForm
                // A new list after a pairing remounts the form, so it doesn't keep the paired Players' ids.
                key={unpaired.map((participant) => participant.playerId).join()}
                leagueId={league.id}
                mixed={league.type === "DoublesMix"}
                players={unpaired.map(({ playerId, name, gender }) => ({ playerId, name, gender }))}
              />
            )}
          </div>
        </Card>
      )}
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
