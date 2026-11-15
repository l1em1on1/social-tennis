import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Field, Input } from "@/components/ui/field";
import { Sheet } from "@/components/ui/sheet";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { SubmitButton } from "@/components/ui/submit-button";
import { approveClubMember, inviteToClub, removeClubMember, withdrawClubInvite } from "@/lib/actions";
import { apiWithSession } from "@/lib/api/server";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "Manage" };

/**
 * Manager area. Today: who is in each Club. Requests to join are approved or
 * declined here, members removed, and an email let in ahead of time (or that
 * invite taken back). League setup (#5),
 * starting the ladder (#7), the to-do queue and month close land here later.
 *
 * Hidden from the nav for non-Managers, and refused here too, since a URL can
 * be typed. The API is the real gate: it refuses each call by role.
 */
export default async function ManagePage({ searchParams }: PageProps<"/manage">) {
  const [viewer, api, { error, invited }] = await Promise.all([getViewer(), apiWithSession(), searchParams]);
  if (!viewer.roles.includes("Manager")) {
    notFound();
  }

  const { data: clubList } = await api.GET("/clubs");
  if (!clubList) {
    throw new Error("GET /clubs failed");
  }
  const clubs = await Promise.all(
    clubList.clubs.map(async (club) => {
      const { data } = await api.GET("/clubs/{clubId}/members", { params: { path: { clubId: club.id } } });
      if (!data) {
        throw new Error(`GET /clubs/${club.id}/members failed`);
      }
      return { ...club, ...data };
    }),
  );

  return (
    <>
      <h1 className="text-2xl font-bold">Manage</h1>

      {error === "email" && (
        <ErrorState title="That email address didn't work" description="Check it and try again." />
      )}
      {typeof invited === "string" && (
        <p role="status" className="rounded-2xl border border-border bg-surface-muted px-4 py-3 text-sm">
          <strong>{invited}</strong> is invited. They are in the club as soon as they sign in and save their name.
        </p>
      )}

      {clubs.length === 0 && <EmptyState title="No clubs yet" description="An Admin sets up the club." />}

      {clubs.map((club) => {
        const waiting = club.members.filter((member) => !member.approved);
        const members = club.members.filter((member) => member.approved);

        return (
          <Card key={club.id} title={club.name}>
            <section aria-labelledby={`waiting-${club.id}`} className="flex flex-col gap-2">
              <h3 id={`waiting-${club.id}`} className="font-semibold">
                Asking to join ({waiting.length})
              </h3>
              {waiting.length === 0 ? (
                <p className="text-sm text-fg-muted">Nobody is waiting.</p>
              ) : (
                <ul className="divide-y divide-border">
                  {waiting.map((member) => (
                    <li key={member.playerId} className="flex flex-col gap-2 py-3">
                      <div>
                        <p className="font-medium">{member.name}</p>
                        <p className="text-sm break-all text-fg-muted">{member.email}</p>
                      </div>
                      <div className="flex flex-wrap gap-2">
                        <form action={approveClubMember}>
                          <input type="hidden" name="clubId" value={club.id} />
                          <input type="hidden" name="playerId" value={member.playerId} />
                          <SubmitButton pendingLabel="Approving…" aria-label={`Approve ${member.name}`}>
                            Approve
                          </SubmitButton>
                        </form>
                        <form action={removeClubMember}>
                          <input type="hidden" name="clubId" value={club.id} />
                          <input type="hidden" name="playerId" value={member.playerId} />
                          <SubmitButton
                            variant="secondary"
                            pendingLabel="Declining…"
                            aria-label={`Decline ${member.name}`}
                          >
                            Decline
                          </SubmitButton>
                        </form>
                      </div>
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
                <ul className="divide-y divide-border">
                  {members.map((member) => (
                    <li key={member.playerId} className="flex items-center justify-between gap-3 py-2">
                      <div>
                        <p className="font-medium">{member.name}</p>
                        <p className="text-sm break-all text-fg-muted">{member.email}</p>
                      </div>
                      {/* Behind a sheet: removing is one tap away from a name in a list. */}
                      <Sheet
                        title={`Remove ${member.name}?`}
                        trigger={
                          <>
                            Remove<span className="sr-only"> {member.name}</span>
                          </>
                        }
                      >
                        <p className="text-fg-muted">
                          {member.name} will no longer be a member of {club.name}. If their email was invited, the
                          invite is withdrawn too. They can ask to join again.
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
              )}
            </section>

            <section aria-labelledby={`invite-${club.id}`} className="mt-5 flex flex-col gap-3">
              <h3 id={`invite-${club.id}`} className="font-semibold">
                Invite by email
              </h3>
              <form action={inviteToClub} className="flex flex-col gap-3">
                <input type="hidden" name="clubId" value={club.id} />
                <Field
                  id={`invite-email-${club.id}`}
                  label="Email address"
                  hint="They join without needing approval. No email is sent: tell them to sign in with this address."
                >
                  {(control) => <Input {...control} name="email" type="email" required autoComplete="off" />}
                </Field>
                <SubmitButton variant="secondary" pendingLabel="Inviting…">
                  Invite
                </SubmitButton>
              </form>
              {club.invites.length > 0 && (
                <ul aria-label="Invited addresses" className="divide-y divide-border">
                  {club.invites.map((invite) => (
                    <li key={invite.id} className="flex flex-col gap-2 py-3">
                      <div className="flex items-center justify-between gap-3">
                        <span className="text-sm break-all">{invite.email}</span>
                        {invite.joined ? <Badge tone="accent">Joined</Badge> : <Badge>Not joined yet</Badge>}
                      </div>
                      <form action={withdrawClubInvite}>
                        <input type="hidden" name="clubId" value={club.id} />
                        <input type="hidden" name="inviteId" value={invite.id} />
                        <SubmitButton
                          variant="secondary"
                          pendingLabel="Withdrawing…"
                          aria-label={`Withdraw invite for ${invite.email}`}
                        >
                          Withdraw invite
                        </SubmitButton>
                      </form>
                    </li>
                  ))}
                </ul>
              )}
              {club.invites.some((invite) => invite.joined) && (
                <p className="text-sm text-fg-muted">
                  Withdrawing an invite keeps the members it let in. To take someone out, remove them above.
                </p>
              )}
            </section>
          </Card>
        );
      })}
    </>
  );
}
