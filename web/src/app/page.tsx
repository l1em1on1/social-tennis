import { api } from "@/lib/api/client";

// Rendered per request: the club list comes from the API (and ultimately
// Postgres) at request time, never from a build-time snapshot.
export const dynamic = "force-dynamic";

export default async function Home() {
  const { data: clubs, error } = await api.GET("/clubs");

  return (
    <main className="mx-auto flex min-h-screen max-w-md flex-col gap-6 p-6">
      <h1 className="text-2xl font-bold">Social Tennis</h1>

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
