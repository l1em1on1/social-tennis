import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ClubNameForm } from "@/components/club-name-form";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Pager, pageRange } from "@/components/ui/pager";
import { SearchForm } from "@/components/ui/search-form";
import { Sheet } from "@/components/ui/sheet";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { SubmitButton } from "@/components/ui/submit-button";
import { deleteUser, grantRole, revokeRole } from "@/lib/actions";
import { adminHref } from "@/lib/admin-href";
import { apiWithSession } from "@/lib/api/server";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "Admin" };

const errors: Record<string, { title: string; description: string }> = {
  name: { title: "That club name didn't work", description: "Enter a name of up to 200 characters." },
  "admin-kept": {
    title: "The Admin role wasn't removed",
    description: "That person is the only Admin, or is an Admin in the app's configuration.",
  },
  search: { title: "That search didn't work", description: "Search with letters, numbers, spaces and punctuation." },
  "user-kept": {
    title: "That user wasn't removed",
    description: "They're you, the only Admin, or an Admin in the app's configuration.",
  },
};

/** Users per page (#134). The API allows up to 100. */
const USERS_PER_PAGE = 50;

/** The API reads the offset as an int32 and refuses a larger one (400). */
const MAX_OFFSET = 2_147_483_647;

/**
 * Clubs and roles (#18): an Admin creates and renames Clubs, and makes Users
 * Managers or Admins. Admins only — refused here as well as hidden from the
 * nav, and the API refuses each call by role. Users come a page at a time,
 * newest first, searchable by email or Player name (#134).
 */
export default async function AdminPage({ searchParams }: PageProps<"/admin">) {
  const [viewer, api, { error, q, offset }] = await Promise.all([getViewer(), apiWithSession(), searchParams]);
  if (!viewer.roles.includes("Admin")) {
    notFound();
  }
  const search = typeof q === "string" ? q.trim() : "";
  const start = Math.min(
    MAX_OFFSET,
    Math.max(0, Number.parseInt(typeof offset === "string" ? offset : "", 10) || 0),
  );

  const [{ data: clubList }, { data: userList, response: usersResponse }] = await Promise.all([
    api.GET("/clubs"),
    api.GET("/users", { params: { query: { q: search || undefined, offset: start, limit: USERS_PER_PAGE } } }),
  ]);
  // A 400 is a search the API won't run (a control character, say): say so, list no one.
  // The offset is kept in range above, so without a search a 400 is a fault, not a refusal.
  const searchRefused = search !== "" && usersResponse.status === 400;
  if (!clubList || (!userList && !searchRefused)) {
    throw new Error("The Admin page's data didn't load");
  }
  const users = userList?.users ?? [];
  // The schema allows a numeric string for an int32 (System.Text.Json reads either); the API sends a number.
  const total = Number(userList?.page.total ?? 0);
  // Where the last page starts. From past the end (a hand-typed offset), Previous goes there.
  const lastPageStart = Math.max(0, Math.floor((total - 1) / USERS_PER_PAGE) * USERS_PER_PAGE);
  const problem = searchRefused ? errors.search : typeof error === "string" ? errors[error] : undefined;
  // A refused role change redirects with its error; these bring it back to this search and page.
  const keepView = (
    <>
      <input type="hidden" name="q" value={search} />
      <input type="hidden" name="offset" value={start} />
    </>
  );

  return (
    <>
      <h1 className="text-2xl font-bold">Admin</h1>

      {problem && <ErrorState title={problem.title} description={problem.description} />}

      <Card title="Clubs">
        {clubList.clubs.length === 0 ? (
          <EmptyState title="No clubs yet" description="Create the club so players can ask to join it." />
        ) : (
          <ul className="-my-2 divide-y divide-border">
            {clubList.clubs.map((club) => (
              // Keyed by the name too: a rename remounts the row, so its sheet starts closed with the new name.
              <li key={`${club.id}:${club.name}`} className="flex min-h-14 items-center justify-between gap-3 py-2">
                <span className="font-medium">{club.name}</span>
                <Sheet
                  title={`Rename ${club.name}`}
                  trigger={
                    <>
                      Rename<span className="sr-only"> {club.name}</span>
                    </>
                  }
                >
                  <ClubNameForm id={`club-name-${club.id}`} submitLabel="Save name" club={club} />
                </Sheet>
              </li>
            ))}
          </ul>
        )}

        {/* Keyed by the count: creating a Club remounts the sheet, so it starts empty next time. */}
        <div className="mt-4" key={clubList.clubs.length}>
          <Sheet title="Create a club" trigger="Create a club">
            <ClubNameForm id="new-club-name" submitLabel="Create club" />
          </Sheet>
        </div>
      </Card>

      <Card title={`Users (${total})`}>
        <p className="mb-3 text-sm text-fg-muted">
          Managers approve who joins a club and run its leagues. Admins can also do everything on this page. A change
          takes effect straight away. Removing someone deletes their account; their players stay in results as
          &ldquo;Former player&rdquo;.
        </p>
        <div className="mb-3">
          <SearchForm
            action="/admin"
            id="user-search"
            label="Find a user"
            hint="Part of an email address or a player's name."
            defaultValue={search}
            maxLength={320}
          />
        </div>
        <p role="status" className="mb-2 text-sm text-fg-muted">
          {users.length > 0
            ? `${pageRange(start, users.length, total)}${search ? ` matching “${search}”` : ""}, newest first.`
            : total > 0
              ? "No users on this page."
              : search
                ? `No one matches “${search}”.`
                : "No users yet."}
          {search && (
            <>
              {" "}
              <Link href="/admin" className="underline">
                Show everyone
              </Link>
            </>
          )}
        </p>
        <ul className="-my-1 divide-y divide-border">
          {users.map((user) => {
            const you = user.id === viewer.userId;
            const who = user.players.length > 0 ? user.players.map((player) => player.name).join(", ") : user.email;
            // Nobody removes themselves, an Admin by configuration, or the last Admin (#116).
            const removable = !you && !user.adminBySetting && !user.lastAdmin;
            return (
              // One line per User (#116): who, their roles, and two icons for what can change. From md,
              // the email and the Players sit side by side, like a table's columns (#225).
              <li key={user.id} className="flex items-center gap-2 py-1.5">
                <div className="min-w-0 flex-1 md:grid md:grid-cols-2 md:items-center md:gap-4">
                  <p className="truncate font-medium">
                    <span>{user.email}</span>
                    {you && <span className="font-normal text-fg-muted"> (you)</span>}
                  </p>
                  <p className="truncate text-sm text-fg-muted">
                    {user.adminBySetting
                      ? "Admin in the app's configuration. It can only be changed where the app is deployed."
                      : user.players.length > 0
                        ? user.players.map((player) => player.name).join(", ")
                        : "No player yet"}
                  </p>
                </div>
                {user.admin && <Badge tone="accent">Admin</Badge>}
                {user.manager && !user.admin && <Badge tone="accent">Manager</Badge>}
                {!user.adminBySetting && (
                  <Sheet title={`Roles for ${who}`} triggerIcon="more" trigger={`Roles for ${user.email}`}>
                    <p className="text-fg-muted">
                      Managers approve who joins a club and run its leagues. An Admin can also create clubs and change
                      anyone&apos;s roles, including yours.
                    </p>
                    {user.admin ? (
                      user.lastAdmin ? (
                        <>
                          <Button variant="secondary" fullWidth disabled aria-describedby={`last-admin-${user.id}`}>
                            Remove admin
                          </Button>
                          <p id={`last-admin-${user.id}`} className="text-sm text-fg-muted">
                            The only Admin. Make someone else an Admin first.
                          </p>
                        </>
                      ) : (
                        <form action={revokeRole}>
                          <input type="hidden" name="userId" value={user.id} />
                          <input type="hidden" name="role" value="Admin" />
                          {keepView}
                          {/* The spoken name starts with the visible text (WCAG 2.5.3) and adds who. */}
                          <SubmitButton variant="secondary" fullWidth pendingLabel="Removing…">
                            Remove admin<span className="sr-only"> from {who}</span>
                          </SubmitButton>
                        </form>
                      )
                    ) : (
                      <>
                        <form action={user.manager ? revokeRole : grantRole}>
                          <input type="hidden" name="userId" value={user.id} />
                          <input type="hidden" name="role" value="Manager" />
                          {keepView}
                          <SubmitButton variant="secondary" fullWidth pendingLabel="Saving…">
                            {user.manager ? "Remove manager" : "Make manager"}
                            <span className="sr-only">
                              {user.manager ? " from " : " of "}
                              {who}
                            </span>
                          </SubmitButton>
                        </form>
                        <form action={grantRole}>
                          <input type="hidden" name="userId" value={user.id} />
                          <input type="hidden" name="role" value="Admin" />
                          {keepView}
                          <SubmitButton fullWidth pendingLabel="Saving…">
                            Make admin<span className="sr-only"> of {who}</span>
                          </SubmitButton>
                        </form>
                      </>
                    )}
                  </Sheet>
                )}
                {removable && (
                  <Sheet title={`Remove ${user.email}?`} triggerIcon="trash" triggerTone="danger" trigger={`Remove ${user.email}`}>
                    <p className="text-fg-muted">
                      Their players show as &ldquo;Former player&rdquo; in results, so nobody else&apos;s scores change.
                      They leave their clubs, and their sign-in ends. They can sign up again with this email, as a new
                      account. This can&apos;t be undone.
                    </p>
                    <form action={deleteUser}>
                      <input type="hidden" name="userId" value={user.id} />
                      {keepView}
                      <SubmitButton variant="danger" fullWidth pendingLabel="Removing…">
                        Remove user
                      </SubmitButton>
                    </form>
                  </Sheet>
                )}
              </li>
            );
          })}
        </ul>
        <div className="mt-4">
          <Pager
            label="users"
            previousHref={
              start > 0
                ? adminHref({ search, offset: Math.max(0, Math.min(start - USERS_PER_PAGE, lastPageStart)) })
                : undefined
            }
            nextHref={start + users.length < total ? adminHref({ search, offset: start + USERS_PER_PAGE }) : undefined}
          />
        </div>
      </Card>
    </>
  );
}
