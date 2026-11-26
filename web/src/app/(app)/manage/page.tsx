import type { Metadata } from "next";
import { notFound } from "next/navigation";
import Link from "next/link";
import { CopyField } from "@/components/copy-field";
import { Badge } from "@/components/ui/badge";
import { ButtonLink } from "@/components/ui/button";
import { ButtonRow } from "@/components/ui/button-stack";
import { Card } from "@/components/ui/card";
import { Field, Input } from "@/components/ui/field";
import { SubmitIconButton } from "@/components/ui/icon-button";
import { Sheet } from "@/components/ui/sheet";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { SubmitButton } from "@/components/ui/submit-button";
import {
  addToClub,
  approveClubMember,
  grantLeagueManagerRole,
  removeClubMember,
  removeLeagueManagerRole,
  resetJoinLink,
  turnOffJoinLink,
  turnOnJoinLink,
  withdrawClubInvite,
} from "@/lib/actions";
import type { components } from "@/lib/api/schema";
import { apiWithSession } from "@/lib/api/server";
import { joinPath } from "@/lib/join";
import { describeStructure, formatDates, typeLabel } from "@/lib/league";
import { publicUrl } from "@/lib/session";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "Manage" };

const errors: Record<string, { title: string; description: string }> = {
  email: { title: "That email address didn't work", description: "Check it and try again." },
  player: { title: "That player wasn't added", description: "Check the email address, and keep the name to 100 characters." },
};

/** Members by the email of the login holding them, in the order the logins first appear. */
function groupByEmail<T extends { email: string }>(members: T[]): [string, T[]][] {
  const groups = new Map<string, T[]>();
  for (const member of members) {
    groups.set(member.email, [...(groups.get(member.email) ?? []), member]);
  }
  return [...groups];
}

/** The Leagues of a Club the caller runs, each opening its own page (#5), and a new one for whoever may create it (#220). */
function ClubLeagues({
  club,
}: {
  club: { id: string; canManage: boolean; canCreateLeagues: boolean; leagues: components["schemas"]["ClubLeague"][] };
}) {
  return (
    <section aria-labelledby={`leagues-${club.id}`} className="flex flex-col gap-2">
      <h3 id={`leagues-${club.id}`} className="font-semibold">
        Leagues ({club.leagues.length})
      </h3>
      {club.leagues.length === 0 ? (
        <p className="text-sm text-fg-muted">
          {club.canManage ? "No leagues yet." : "You don't run a league here yet."}
        </p>
      ) : (
        <ul className="divide-y divide-border">
          {club.leagues.map((league) => (
            <li key={league.id} className="flex flex-col gap-1 py-3">
              <div className="flex items-center justify-between gap-3">
                <Link href={`/manage/leagues/${league.id}`} className="font-medium text-accent underline">
                  {league.name}
                </Link>
                <div className="flex gap-1">
                  {league.stage && <Badge tone="success">Started</Badge>}
                  {Number(league.pendingRequestCount) > 0 && (
                    <Badge tone="warning">{`${league.pendingRequestCount} asking`}</Badge>
                  )}
                </div>
              </div>
              <p className="text-sm text-fg-muted">
                {typeLabel(league.type)} · {describeStructure(league)}
              </p>
              <p className="text-sm text-fg-muted">
                {formatDates(league.startDate, league.endDate)} · {league.participantCount} in
              </p>
            </li>
          ))}
        </ul>
      )}
      {club.canCreateLeagues && (
        <ButtonLink href={`/manage/leagues/new?club=${club.id}`} variant="secondary">
          New league
        </ButtonLink>
      )}
    </section>
  );
}

/** Who has a role in a Club (#110, #220): its managers, whom an Admin chooses, and the League managers its managers choose. */
function ClubManagers({ club }: { club: { id: string; roles: components["schemas"]["ClubRoleSummary"][] } }) {
  return (
    <section aria-labelledby={`managers-${club.id}`} className="mt-5 flex flex-col gap-2">
      <h3 id={`managers-${club.id}`} className="font-semibold">
        Managers ({club.roles.length})
      </h3>
      <p className="text-sm text-fg-muted">
        Managers run the club, and an Admin chooses them. League managers create leagues, and run those they create or
        are given.
      </p>
      <ul className="divide-y divide-border">
        {club.roles.map((role) => (
          <li key={role.userId} className="flex items-center gap-2 py-1.5">
            {/* The email is all that says who they are: never cut short. */}
            <div className="flex min-w-0 flex-1 flex-col items-start gap-1">
              <p className="text-sm break-all">{role.email}</p>
              <Badge tone={role.role === "Manager" ? "accent" : undefined}>
                {role.role === "Manager" ? "Manager" : "League manager"}
              </Badge>
            </div>
            {role.role === "LeagueManager" && (
              <form action={removeLeagueManagerRole}>
                <input type="hidden" name="clubId" value={club.id} />
                <input type="hidden" name="userId" value={role.userId} />
                <SubmitIconButton icon="cross" tone="danger" label={`Remove ${role.email} as league manager`} />
              </form>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}

/**
 * Manager area: each Club's Leagues (each opens its own page, #5), and who is
 * in the Club, for its managers; a League manager (#220) has only the Leagues
 * they run. The Club's join link is shared, reset and turned off here (#222),
 * and the requests it brings approved or declined; someone is added by email,
 * with a name to add their Player now or without one to pre-approve the email
 * (#224), members removed, and a pre-approval taken back. Starting the ladder (#7), the to-do queue and month
 * close land here later. Rating is hidden from the UI for now (#161, ADR-0007).
 *
 * Hidden from the nav for non-Managers, and refused here too, since a URL can
 * be typed. The API is the real gate: it refuses each call by role.
 */
export default async function ManagePage({ searchParams }: PageProps<"/manage">) {
  const [viewer, api, { error, invited, added }] = await Promise.all([getViewer(), apiWithSession(), searchParams]);
  const problem = typeof error === "string" ? errors[error] : undefined;
  if (!viewer.manages) {
    notFound();
  }

  const { data: clubList } = await api.GET("/clubs");
  if (!clubList) {
    throw new Error("GET /clubs failed");
  }
  const clubs = await Promise.all(
    clubList.clubs.map(async (club) => {
      const params = { params: { path: { clubId: club.id } } };
      // A League manager (#220) sees their own Leagues in a Club, and nothing of its members.
      if (!club.canManage) {
        const { data: leagueList } = await api.GET("/clubs/{clubId}/leagues", params);
        if (!leagueList) {
          throw new Error(`GET /clubs/${club.id}/leagues failed`);
        }
        return { ...club, leagues: leagueList.leagues, members: [], invites: [], roles: [], joinCode: null };
      }
      const [{ data }, { data: leagueList }, { data: joinLink }] = await Promise.all([
        api.GET("/clubs/{clubId}/members", params),
        api.GET("/clubs/{clubId}/leagues", params),
        api.GET("/clubs/{clubId}/join-link", params),
      ]);
      if (!data || !leagueList || !joinLink) {
        throw new Error(`GET /clubs/${club.id}/members, /leagues or /join-link failed`);
      }
      return { ...club, ...data, leagues: leagueList.leagues, joinCode: joinLink.code ?? null };
    }),
  );

  return (
    <>
      <h1 className="text-4xl leading-none font-black">Manage</h1>

      {problem && <ErrorState title={problem.title} description={problem.description} />}
      {typeof added === "string" && (
        <p role="status" className="rounded-md border border-border bg-surface-muted px-4 py-3 text-sm">
          <strong>{added}</strong> is added.
        </p>
      )}
      {typeof invited === "string" && (
        <p role="status" className="rounded-md border border-border bg-surface-muted px-4 py-3 text-sm">
          <strong>{invited}</strong> is pre-approved. If they already have a player, they&apos;re in the club now; if not,
          they&apos;re in as soon as they sign in with that address and save their name. We don&apos;t email them, so let
          them know.
        </p>
      )}

      {clubs.length === 0 && <EmptyState title="No clubs yet" description="An Admin sets up the club." />}

      {clubs.map((club) => {
        const waiting = club.members.filter((member) => !member.approved);
        const members = club.members.filter((member) => member.approved);

        if (!club.canManage) {
          return (
            <Card key={club.id} title={club.name}>
              <ClubLeagues club={club} />
            </Card>
          );
        }
        const roleOf = (userId: string) => club.roles.find((role) => role.userId === userId)?.role;

        return (
          <Card key={club.id} title={club.name}>
            {/* From md, two columns (#225): the Club's Leagues and link | who's in it. */}
            <div className="md:grid md:grid-cols-2 md:gap-x-8">
              <div>
                <ClubLeagues club={club} />

                {/* One shared link per Club (#222, ADR-0032): opening it lets someone ask to join, approved below. */}
                <section aria-labelledby={`join-link-${club.id}`} className="mt-5 flex flex-col gap-2">
                  <h3 id={`join-link-${club.id}`} className="font-semibold">
                    Join link
                  </h3>
                  {club.joinCode ? (
                    <>
                      <CopyField
                        id={`join-link-url-${club.id}`}
                        label={`Link to ask to join ${club.name}`}
                        hint="Share it in the club's chat or by email. Whoever opens it can ask to join, and you approve them below."
                        value={publicUrl(joinPath(club.joinCode))}
                      />
                      <ButtonRow>
                        <form action={resetJoinLink}>
                          <input type="hidden" name="clubId" value={club.id} />
                          <SubmitButton variant="secondary" pendingLabel="Resetting…">
                            Reset link
                          </SubmitButton>
                        </form>
                        <form action={turnOffJoinLink}>
                          <input type="hidden" name="clubId" value={club.id} />
                          <SubmitButton variant="secondary" pendingLabel="Turning off…">
                            Turn off
                          </SubmitButton>
                        </form>
                      </ButtonRow>
                      <p className="text-sm text-fg-muted">Resetting makes a new link, and the old one stops working.</p>
                    </>
                  ) : (
                    <>
                      <p className="text-sm text-fg-muted">
                        Off. Turn it on for a link you can share: whoever opens it can ask to join.
                      </p>
                      <form action={turnOnJoinLink}>
                        <input type="hidden" name="clubId" value={club.id} />
                        <SubmitButton variant="secondary" pendingLabel="Turning on…">
                          Turn on the join link
                        </SubmitButton>
                      </form>
                    </>
                  )}
                </section>
              </div>
              <div>
                <section aria-labelledby={`waiting-${club.id}`} className="mt-5 flex flex-col gap-2 md:mt-0">
                  <h3 id={`waiting-${club.id}`} className="font-semibold">
                    Asking to join ({waiting.length})
                  </h3>
                  {waiting.length === 0 ? (
                    <p className="text-sm text-fg-muted">Nobody is waiting.</p>
                  ) : (
                    <ul className="divide-y divide-border">
                      {waiting.map((member) => (
                        // One line: who, and approve or decline beside them (#224).
                        <li key={member.playerId} className="flex items-center gap-2 py-1.5">
                          <div className="min-w-0 flex-1">
                            <p className="truncate font-medium">{member.name}</p>
                            <p className="truncate text-sm text-fg-muted">{member.email}</p>
                          </div>
                          <form action={approveClubMember}>
                            <input type="hidden" name="clubId" value={club.id} />
                            <input type="hidden" name="playerId" value={member.playerId} />
                            <SubmitIconButton icon="check" tone="accent" label={`Approve ${member.name}`} />
                          </form>
                          <form action={removeClubMember}>
                            <input type="hidden" name="clubId" value={club.id} />
                            <input type="hidden" name="playerId" value={member.playerId} />
                            <SubmitIconButton icon="cross" tone="danger" label={`Decline ${member.name}`} />
                          </form>
                        </li>
                      ))}
                    </ul>
                  )}
                </section>

                <section aria-labelledby={`members-${club.id}`} className="mt-5 flex flex-col gap-2">
                  <h3 id={`members-${club.id}`} className="font-semibold">
                    Members ({members.length})
                  </h3>
                  {members.length === 0 ? (
                    <p className="text-sm text-fg-muted">No members yet.</p>
                  ) : (
                    // One entry per login, with the Players it holds: a parent and their children sit together.
                    <ul className="divide-y divide-border">
                      {groupByEmail(members).map(([email, held]) => (
                        <li key={email} className="flex flex-col py-2">
                          <div className="flex items-center gap-2">
                            <p className="min-w-0 flex-1 truncate text-sm text-fg-muted">{email}</p>
                            {!held[0].signedIn && <Badge>Not signed in yet</Badge>}
                            {roleOf(held[0].userId) === "Manager" && <Badge tone="accent">Manager</Badge>}
                            {roleOf(held[0].userId) === "LeagueManager" && <Badge>League manager</Badge>}
                            {!roleOf(held[0].userId) && (
                              <Sheet
                                title={`Role for ${held.map((member) => member.name).join(", ")}`}
                                triggerIcon="more"
                                trigger={`Role for ${email}`}
                              >
                                <p className="text-fg-muted">
                                  A league manager creates leagues at {club.name}, and runs those they create or are given.
                                  They don&apos;t manage the club or its members.
                                </p>
                                <form action={grantLeagueManagerRole}>
                                  <input type="hidden" name="clubId" value={club.id} />
                                  <input type="hidden" name="userId" value={held[0].userId} />
                                  <SubmitButton fullWidth pendingLabel="Saving…">
                                    Make league manager
                                    <span className="sr-only"> of {club.name} ({email})</span>
                                  </SubmitButton>
                                </form>
                              </Sheet>
                            )}
                          </div>
                          <ul aria-label={`Players of ${email}`} className="flex flex-col">
                            {held.map((member) => (
                              // One line: the name, its level and gender, and removing it (#224).
                              <li key={member.playerId} className="flex items-center gap-2">
                                <p className="min-w-0 flex-1 truncate">
                                  <span className="font-medium">{member.name}</span>
                                  <span className="text-sm text-fg-muted">
                                    {" · "}
                                    {[member.level === null ? null : `Level ${member.level}`, member.gender]
                                      .filter(Boolean)
                                      .join(" · ") || "Level and gender not set"}
                                  </span>
                                </p>
                                {/* Behind a sheet: removing is one tap away from a name in a list. */}
                                <Sheet title={`Remove ${member.name}?`} triggerIcon="trash" triggerTone="danger" trigger={`Remove ${member.name}`}>
                                  <p className="text-fg-muted">
                                    {member.name} will no longer be a member of {club.name}. They can ask to join again.
                                  </p>
                                  <form action={removeClubMember}>
                                    <input type="hidden" name="clubId" value={club.id} />
                                    <input type="hidden" name="playerId" value={member.playerId} />
                                    <SubmitButton variant="danger" fullWidth pendingLabel="Removing…">
                                      Remove from club
                                    </SubmitButton>
                                  </form>
                                </Sheet>
                              </li>
                            ))}
                          </ul>
                        </li>
                      ))}
                    </ul>
                  )}
                </section>

                <ClubManagers club={club} />

                {/* One form for both ways in (#224): a name adds that Player now, none pre-approves the email. */}
                <section aria-labelledby={`add-${club.id}`} className="mt-5 flex flex-col gap-3">
                  <h3 id={`add-${club.id}`} className="font-semibold">
                    Add someone
                  </h3>
                  <form action={addToClub} className="flex flex-col gap-3">
                    <input type="hidden" name="clubId" value={club.id} />
                    <Field id={`add-email-${club.id}`} label="Their email address">
                      {(control) => <Input {...control} name="email" type="email" required autoComplete="off" />}
                    </Field>
                    <Field
                      id={`add-name-${club.id}`}
                      label="Name (optional)"
                      hint="With a name, that player is added now: use a parent's address for a child. Without one, they join with their own players once they sign in. Any players this address has already are let in too. We don't email them, so let them know."
                    >
                      {(control) => <Input {...control} name="name" maxLength={100} autoComplete="off" />}
                    </Field>
                    <SubmitButton variant="secondary" pendingLabel="Adding…">
                      Add
                    </SubmitButton>
                  </form>
                  {club.invites.length > 0 && (
                    <>
                      <h4 className="text-sm font-semibold">Pre-approved, no player yet</h4>
                      <ul aria-label="Pre-approved addresses" className="divide-y divide-border">
                        {club.invites.map((invite) => (
                          <li key={invite.id} className="flex items-center gap-2 py-0.5">
                            <span className="min-w-0 flex-1 truncate text-sm">{invite.email}</span>
                            <form action={withdrawClubInvite}>
                              <input type="hidden" name="clubId" value={club.id} />
                              <input type="hidden" name="inviteId" value={invite.id} />
                              <SubmitIconButton icon="cross" tone="danger" label={`Withdraw pre-approval for ${invite.email}`} />
                            </form>
                          </li>
                        ))}
                      </ul>
                    </>
                  )}
                </section>
              </div>
            </div>
          </Card>
        );
      })}
    </>
  );
}
