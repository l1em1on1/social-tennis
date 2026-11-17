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
import { grantRole, revokeRole } from "@/lib/actions";
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
};

/** Users per page (#134). The API allows up to 100. */
const USERS_PER_PAGE = 50;

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
  const start = Math.max(0, Number.parseInt(typeof offset === "string" ? offset : "", 10) || 0);

  const [{ data: clubList }, { data: userList, response: usersResponse }] = await Promise.all([
    api.GET("/clubs"),
    api.GET("/users", { params: { query: { q: search || undefined, offset: start, limit: USERS_PER_PAGE } } }),
  ]);
  // A 400 is a search the API won't run (a control character, say): say so, list no one.
  const searchRefused = usersResponse.status === 400;
  if (!clubList || (!userList && !searchRefused)) {
    throw new Error("The Admin page's data didn't load");
  }
  const users = userList?.users ?? [];
  // The schema allows a numeric string for an int32 (System.Text.Json reads either); the API sends a number.
  const total = Number(userList?.page.total ?? 0);
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
          takes effect straight away.
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
            : search
              ? `No one matches “${search}”.`
              : total > 0
                ? "No users on this page."
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
        <ul className="-my-2 divide-y divide-border">
          {users.map((user) => {
            const you = user.id === viewer.userId;
            const who = user.players.length > 0 ? user.players.map((player) => player.name).join(", ") : user.email;
            return (
              <li key={user.id} className="flex flex-col gap-2 py-3">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="font-medium break-all">
                      <span>{user.email}</span>
                      {you && <span className="font-normal text-fg-muted"> (you)</span>}
                    </p>
                    <p className="text-sm text-fg-muted">
                      {user.players.length > 0
                        ? user.players.map((player) => player.name).join(", ")
                        : "No player yet"}
                    </p>
                  </div>
                  <div className="flex shrink-0 flex-col items-end gap-1">
                    {user.admin && <Badge tone="accent">Admin</Badge>}
                    {user.manager && !user.admin && <Badge tone="accent">Manager</Badge>}
                  </div>
                </div>

                {user.admin ? (
                  user.adminBySetting ? (
                    <p className="text-sm text-fg-muted">
                      Admin in the app&apos;s configuration. It can only be changed where the app is deployed.
                    </p>
                  ) : user.lastAdmin ? (
                    <div className="flex flex-col gap-2">
                      <Button variant="secondary" disabled aria-describedby={`last-admin-${user.id}`}>
                        Remove admin
                      </Button>
                      <p id={`last-admin-${user.id}`} className="text-sm text-fg-muted">
                        The only Admin. Make someone else an Admin first.
                      </p>
                    </div>
                  ) : (
                    <form action={revokeRole}>
                      <input type="hidden" name="userId" value={user.id} />
                      <input type="hidden" name="role" value="Admin" />
                      {keepView}
                      {/* The spoken name starts with the visible text (WCAG 2.5.3) and adds who. */}
                      <SubmitButton variant="secondary" pendingLabel="Removing…">
                        Remove admin<span className="sr-only"> from {who}</span>
                      </SubmitButton>
                    </form>
                  )
                ) : (
                  <div className="flex flex-wrap gap-2">
                    <form action={user.manager ? revokeRole : grantRole}>
                      <input type="hidden" name="userId" value={user.id} />
                      <input type="hidden" name="role" value="Manager" />
                      {keepView}
                      <SubmitButton variant="secondary" pendingLabel="Saving…">
                        {user.manager ? "Remove manager" : "Make manager"}
                        <span className="sr-only">
                          {user.manager ? " from " : " of "}
                          {who}
                        </span>
                      </SubmitButton>
                    </form>
                    {/* Behind a sheet: an Admin can change everyone's roles, this one's included. */}
                    <Sheet
                      title={`Make ${who} an Admin?`}
                      trigger={
                        <>
                          Make admin<span className="sr-only"> of {who}</span>
                        </>
                      }
                    >
                      <p className="text-fg-muted">
                        An Admin can create clubs and change anyone&apos;s roles, including yours. They can also do
                        everything a Manager can.
                      </p>
                      <form action={grantRole}>
                        <input type="hidden" name="userId" value={user.id} />
                        <input type="hidden" name="role" value="Admin" />
                        <SubmitButton fullWidth pendingLabel="Saving…">
                          Make admin
                        </SubmitButton>
                      </form>
                    </Sheet>
                  </div>
                )}
              </li>
            );
          })}
        </ul>
        <div className="mt-4">
          <Pager
            label="users"
            previousHref={
              start > 0 ? adminHref({ search, offset: Math.max(0, start - USERS_PER_PAGE) }) : undefined
            }
            nextHref={start + users.length < total ? adminHref({ search, offset: start + USERS_PER_PAGE }) : undefined}
          />
        </div>
      </Card>
    </>
  );
}
