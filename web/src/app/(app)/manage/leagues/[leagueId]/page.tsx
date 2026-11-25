import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { TeamPairingForm } from "@/components/team-pairing-form";
import { Badge } from "@/components/ui/badge";
import { ButtonLink } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Field, Input } from "@/components/ui/field";
import { SubmitIconButton } from "@/components/ui/icon-button";
import { Select } from "@/components/ui/select";
import { Sheet } from "@/components/ui/sheet";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { SubmitButton } from "@/components/ui/submit-button";
import {
  addLeagueParticipant,
  assignLeagueManager,
  moveTeam,
  approveLeagueJoinRequest,
  rejectLeagueJoinRequest,
  removeLeagueParticipant,
  renameLeague,
  unassignLeagueManager,
  unpairTeam,
} from "@/lib/actions";
import type { components } from "@/lib/api/schema";
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
 * without a partner, to pair (#6); then adding a Club member straight in. A
 * manager of its Club also says who else runs it (#220).
 */
export default async function LeaguePage({ params, searchParams }: PageProps<"/manage/leagues/[leagueId]">) {
  const [viewer, api, { leagueId }, { created, error, started: justStarted, closed: justClosed, moved, moveRefused }] = await Promise.all([
    getViewer(),
    apiWithSession(),
    params,
    searchParams,
  ]);
  if (!viewer.manages || !isId(leagueId)) {
    notFound();
  }

  const { data: league, response } = await api.GET("/leagues/{leagueId}", { params: { path: { leagueId } } });
  // 403: a League they can see, but don't run (#110).
  if (response.status === 404 || response.status === 403) {
    notFound();
  }
  if (!league) {
    throw new Error(`GET /leagues/${leagueId} failed with ${response.status}`);
  }
  // What holds up the current Stage's close (#10), as its Players see it on the ladder.
  const { data: ladder } = league.stage
    ? await api.GET("/leagues/{leagueId}/ladder", { params: { path: { leagueId } } })
    : { data: undefined };
  const { data: addableList } = await api.GET("/leagues/{leagueId}/addable-players", { params: { path: { leagueId } } });
  if (!addableList) {
    throw new Error(`GET /leagues/${leagueId}/addable-players failed`);
  }
  // Who could be given the League to run: only a manager of its Club sees its members, and says so (#220).
  const { data: members } = league.canAssignManagers
    ? await api.GET("/clubs/{clubId}/members", { params: { path: { clubId: league.clubId } } })
    : { data: undefined };
  if (league.canAssignManagers && !members) {
    throw new Error(`GET /clubs/${league.clubId}/members failed`);
  }

  const pending = league.joinRequests.filter((request) => request.status === "Pending");
  const decided = league.joinRequests.filter((request) => request.status !== "Pending");
  // Removal keeps a rejected request, and the API refuses a second ask (409), so
  // a Player who was turned down can only be added back.
  const turnedDown = new Set(decided.filter((request) => request.status === "Rejected").map((request) => request.playerId));
  const addable = addableList.players;
  // One per login: each Player they hold in the Club, and not those who run it already.
  const managerIds = new Set(league.managers.map((manager) => manager.userId));
  const couldManage = new Map<string, { names: string[]; email: string }>();
  for (const member of members?.members ?? []) {
    if (!member.approved || managerIds.has(member.userId)) continue;
    const login = couldManage.get(member.userId) ?? { names: [], email: member.email };
    couldManage.set(member.userId, { ...login, names: [...login.names, member.name] });
  }
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
        <div className="flex flex-col gap-2">
          <p className="text-sm">
            <Badge tone="success">Started</Badge>{" "}
            <span className="text-fg-muted">
              Stage {stage.number} runs {formatDates(stage.startDate, stage.endDate)}.
            </span>
          </p>
          <div>
            <ButtonLink href={`/leagues/${league.id}`} variant="secondary">
              See the ladder
            </ButtonLink>
          </div>
          {/* From md, side by side (#225): what holds the close up, and moving a Team. */}
          <div className="flex flex-col gap-2 md:grid md:grid-cols-2 md:items-start md:gap-4">
            {ladder && <ToDo ladder={ladder} />}
            {ladder && !ladder.ended && <MoveTeamCard ladder={ladder} moved={typeof moved === "string" ? moved : undefined} refused={typeof moveRefused === "string" ? moveRefused : undefined} />}
          </div>
        </div>
      ) : (
        canStart && (
          <div>
            <ButtonLink href={`/manage/leagues/${league.id}/start`}>Start the league</ButtonLink>
          </div>
        )
      )}
      {justClosed === "1" && ladder?.stage && (
        <p role="status" className="rounded-2xl border border-success bg-success-muted px-4 py-3 text-sm">
          {ladder.ended
            ? "The League's last Stage is closed: the League has ended."
            : `Stage ${Number(ladder.stage.number) - 1} is closed, and Stage ${Number(ladder.stage.number)} runs ${formatDates(ladder.stage.startDate, ladder.stage.endDate)}.`}
        </p>
      )}
      {justStarted === "1" && stage && (
        <p role="status" className="rounded-2xl border border-border bg-surface-muted px-4 py-3 text-sm">
          <strong>{league.name}</strong> has started. Each Division&apos;s Games are waiting to be played by{" "}
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
      {error === "not-in-club" && (
        <ErrorState
          title="That wasn't given"
          description="Only someone with a player in the club can run one of its leagues, and they may have left it since."
        />
      )}
      {error === "conflict" && (
        <ErrorState
          title="That didn't go through"
          description="Someone else may have decided it first, or the player is no longer a member of the club. The lists below are up to date."
        />
      )}

      {/* From md, the cards sit two by two (#225). */}
      <div className="flex flex-col gap-4 md:grid md:grid-cols-2 md:items-start">
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
              Every member of {league.clubName} is in this league.{" "}
              {league.canAssignManagers
                ? "Add new players to the club on Manage first."
                : "A manager of the club adds new players to it."}
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
                        {member.email ? `${member.name} (${member.email})` : member.name}
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

        <Card title={`Managers (${league.managers.length})`}>
          <p className="text-sm text-fg-muted">
            {league.managers.length === 0
              ? "Run by the club's managers."
              : league.canAssignManagers
                ? "They run this league. The club's managers run it too."
                : "Those who run this league, beside the club's managers."}
          </p>
          {league.managers.length > 0 && (
            <ul aria-label="Managers of this league" className="mt-2 divide-y divide-border">
              {league.managers.map((manager) => {
                const who = manager.name ?? "A manager";
                // Two with no Player in the Club are told apart by email: whoever may take the League back sees it.
                const told = manager.name ?? manager.email ?? "a manager";
                return (
                  <li key={manager.userId} className="flex min-h-12 items-center gap-2 py-1.5">
                    <div className="min-w-0 flex-1">
                      <p className="truncate font-medium">{who}</p>
                      {manager.email && <p className="truncate text-sm text-fg-muted">{manager.email}</p>}
                    </div>
                    {league.canAssignManagers && (
                      <form action={unassignLeagueManager}>
                        <input type="hidden" name="leagueId" value={league.id} />
                        <input type="hidden" name="userId" value={manager.userId} />
                        <SubmitIconButton icon="cross" tone="danger" label={`Remove ${told} as a manager of ${league.name}`} />
                      </form>
                    )}
                  </li>
                );
              })}
            </ul>
          )}
          {league.canAssignManagers &&
            (couldManage.size === 0 ? (
              <p className="mt-3 text-sm text-fg-muted">Everyone with a player in {league.clubName} runs this league.</p>
            ) : (
              <form action={assignLeagueManager} className="mt-3 flex flex-col gap-3">
                <input type="hidden" name="leagueId" value={league.id} />
                <Field id="add-manager" label="Give this league to" hint="Anyone with a player in the club. They run this league, and nothing else in the club.">
                  {(control) => (
                    <Select {...control} name="userId" required defaultValue="">
                      <option value="" disabled>
                        Pick a member…
                      </option>
                      {[...couldManage].map(([userId, login]) => (
                        <option key={userId} value={userId}>
                          {login.names.join(", ")} ({login.email})
                        </option>
                      ))}
                    </Select>
                  )}
                </Field>
                <SubmitButton variant="secondary" pendingLabel="Saving…">
                  Make league manager
                </SubmitButton>
              </form>
            ))}
        </Card>
      </div>

      <ButtonLink href="/manage" variant="ghost" className="md:self-start">
        Back to Manage
      </ButtonLink>
    </>
  );
}

/**
 * What the Manager settles before the Stage can close (#10; wireframe M5):
 * each Game nobody scored, to record or mark not played on its Match screen,
 * and each tie that decides who moves, which they order when they close it.
 */
function ToDo({ ladder }: { ladder: components["schemas"]["GetLeagueLadderResponse"] }) {
  if (ladder.ended) {
    return (
      <Card title="The League has ended" aria-label="To do">
        <p className="text-sm text-fg-muted">Its last Stage is closed. The ladder shows how it finished.</p>
      </Card>
    );
  }

  const competitors = ladder.divisions.flatMap((division) => division.competitors);
  const name = (teamId: string) =>
    competitors.find((competitor) => competitor.teamId === teamId)?.players.map((player) => player.name).join(" & ") ?? "";
  const unscored = ladder.divisions.flatMap((division) =>
    division.games
      .filter((game) => ladder.awaiting.unscoredGameIds.includes(game.id))
      .map((game) => ({ game, division: Number(division.number) })),
  );
  const { ties } = ladder.awaiting;
  return (
    <Card title="To do before the Stage closes" aria-label="To do" aside={ladder.heldUp ? <Badge tone="warning">Held up</Badge> : undefined}>
      {unscored.length === 0 && ties.length === 0 ? (
        <p className="text-sm text-fg-muted">Nothing to settle: every game has a score or is marked not played.</p>
      ) : (
        <div className="flex flex-col gap-3 text-sm">
          {unscored.length > 0 && (
            <div className="flex flex-col gap-1">
              <h3 className="font-semibold">Games with no score</h3>
              <ul aria-label="Games with no score" className="divide-y divide-border">
                {unscored.map(({ game, division }) => (
                  <li key={game.id} className="flex items-center justify-between gap-2 py-1.5">
                    <span className="min-w-0 break-words">
                      {name(game.firstTeamId)} v {name(game.secondTeamId)} <span className="text-fg-muted">· Division {division}</span>
                    </span>
                    <ButtonLink href={`/games/${game.id}`} variant="secondary" className="shrink-0">
                      Settle
                    </ButtonLink>
                  </li>
                ))}
              </ul>
            </div>
          )}
          {ties.length > 0 && (
            <div className="flex flex-col gap-1">
              <h3 className="font-semibold">Ties that decide who moves</h3>
              <ul aria-label="Ties that decide who moves" className="list-disc pl-5">
                {ties.map((tie) => (
                  <li key={`${Number(tie.division)}:${tie.teamIds.join()}`}>
                    Division {Number(tie.division)}: {tie.teamIds.map(name).join(", ")}
                  </li>
                ))}
              </ul>
              <p className="text-fg-muted">You put them in order when you close the Stage.</p>
            </div>
          )}
        </div>
      )}
      <div className="mt-3">
        <ButtonLink href={`/manage/leagues/${ladder.id}/close`} variant={ladder.heldUp ? "primary" : "secondary"}>
          Close Stage {Number(ladder.stage?.number)}
        </ButtonLink>
      </div>
    </Card>
  );
}

/** Why a move was refused, by the API's type (#10). */
const MOVE_REFUSED: Record<string, string> = {
  "team-has-results": "That Team has a result this Stage, so it can't move now: results stay where they were played. It moves at the close instead.",
  "division-too-small": "That would leave its Division with one Team. A Division keeps at least two.",
  "not-on-the-ladder": "That Team isn't on the ladder this Stage: add it when you close the Stage.",
  "stage-closed": "The League has ended, so nobody moves.",
  invalid: "Pick a Team and a different Division.",
  changed: "The ladder changed meanwhile. Look at it again.",
};

/**
 * A Manager's mid-Stage move (#10): a Team on the ladder to another
 * Division, the exceptional movement (say to rebalance after a Withdrawal),
 * by its own path, not the close's Relegation. It starts at the bottom there
 * and plays everyone; its Games in the Division it leaves are dropped.
 */
function MoveTeamCard({ ladder, moved, refused }: { ladder: components["schemas"]["GetLeagueLadderResponse"]; moved?: string; refused?: string }) {
  const teams = ladder.divisions.flatMap((division) =>
    division.competitors.map((competitor) => ({
      teamId: competitor.teamId,
      division: Number(division.number),
      name: competitor.players.map((player) => player.name).join(" & "),
    })),
  );
  const movedTeam = teams.find((team) => team.teamId === moved);
  return (
    <Card title="Move a Team" aria-label="Move a Team">
      <div className="flex flex-col gap-3">
        {movedTeam && (
          <p role="status" className="rounded-xl border border-success bg-success-muted px-3 py-2 text-sm">
            {movedTeam.name} is in Division {movedTeam.division} now, at the bottom, with a Game against everyone there. Its unplayed Games in its old
            Division are gone.
          </p>
        )}
        {refused && MOVE_REFUSED[refused] && <ErrorState title="That move didn't happen" description={MOVE_REFUSED[refused]} />}
        <p className="text-sm text-fg-muted">
          For the exceptional case, such as rebalancing after someone leaves. A Team with a result this Stage moves at the close instead.
        </p>
        <form action={moveTeam} className="flex flex-col gap-3">
          <input type="hidden" name="leagueId" value={ladder.id} />
          <Field id="move-team" label="Team">
            {(control) => (
              <Select {...control} name="teamId" required defaultValue="">
                <option value="" disabled>
                  Pick a Team
                </option>
                {teams.map((team) => (
                  <option key={team.teamId} value={team.teamId}>
                    {team.name} · Division {team.division}
                  </option>
                ))}
              </Select>
            )}
          </Field>
          <Field id="move-division" label="To Division">
            {(control) => (
              <Select {...control} name="toDivision" required defaultValue="">
                <option value="" disabled>
                  Pick a Division
                </option>
                {ladder.divisions.map((division) => (
                  <option key={Number(division.number)} value={Number(division.number)}>
                    Division {Number(division.number)}
                  </option>
                ))}
              </Select>
            )}
          </Field>
          <SubmitButton variant="secondary" pendingLabel="Moving…">
            Move
          </SubmitButton>
        </form>
      </div>
    </Card>
  );
}
