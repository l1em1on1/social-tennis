import { redirect } from "next/navigation";
import { cookies } from "next/headers";
import { apiWithSession } from "@/lib/api/server";
import { SESSION_COOKIE } from "@/lib/session";

// Rendered per request: session check and club list happen at request time,
// never from a build-time snapshot.
export const dynamic = "force-dynamic";

async function logout() {
  "use server";
  const api = await apiWithSession();
  // Revoke server-side first — the credential dies even if a cookie copy
  // survives somewhere — then drop the cookie.
  await api.DELETE("/auth/sessions/current");
  (await cookies()).delete(SESSION_COOKIE);
  redirect("/login");
}

export default async function Home() {
  const api = await apiWithSession();

  // proxy.ts only checks cookie presence; this is the real validity check —
  // a revoked or expired session gets a 401 here and lands back on /login.
  const { data: me, response } = await api.GET("/auth/me");
  if (response.status === 401 || !me) {
    redirect("/login");
  }

  const { data: clubs, error } = await api.GET("/clubs");

  return (
    <main className="mx-auto flex min-h-screen max-w-md flex-col gap-6 p-6">
      <header className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Social Tennis</h1>
        <form action={logout}>
          <button type="submit" className="rounded-lg border px-3 py-1.5 text-sm">
            Log out
          </button>
        </form>
      </header>

      <p className="text-sm text-gray-500">Signed in as {me.email}</p>

      {error || !clubs ? (
        <p role="alert" className="text-red-600">
          The API is not reachable right now.
        </p>
      ) : (
        <section>
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">
            Clubs
          </h2>
          <ul className="divide-y rounded-lg border">
            {clubs.map((club) => (
              <li key={club.id} className="p-4">
                {club.name}
              </li>
            ))}
          </ul>
        </section>
      )}
    </main>
  );
}
