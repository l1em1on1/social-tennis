import { Card } from "@/components/ui/card";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { ButtonLink } from "@/components/ui/button";
import { apiWithSession } from "@/lib/api/server";
import { getViewer } from "@/lib/viewer";

/**
 * Home. #42 turns this into "this month's Games, deadline, Scores owed"; until
 * Leagues exist it says so, and keeps the Club list as its one live API call.
 */
export default async function Home() {
  const viewer = await getViewer();
  const api = await apiWithSession();

  // List endpoints return an envelope, not the array itself (ADR-0012) — the
  // collection is one property of it, alongside `page`.
  const { data: clubList, error } = await api.GET("/clubs");

  return (
    <>
      <h1 className="text-2xl font-bold">Home</h1>

      <Card title="This month">
        <EmptyState
          title="No games yet"
          description={
            viewer.actingPlayer
              ? `${viewer.actingPlayer.name}'s games appear here once a league they're in starts.`
              : "Your games appear here once a league you're in starts."
          }
          action={
            <ButtonLink href="/ladder" variant="secondary">
              View the ladder
            </ButtonLink>
          }
        />
      </Card>

      {error || !clubList ? (
        <ErrorState title="Clubs didn't load" description="The server isn't reachable right now. Try again shortly." />
      ) : (
        <Card title="Clubs">
          {clubList.clubs.length === 0 ? (
            <EmptyState title="No clubs yet" description="An Admin sets up the club." />
          ) : (
            <ul className="-my-2 divide-y divide-border">
              {clubList.clubs.map((club) => (
                <li key={club.id} className="py-3">
                  {club.name}
                </li>
              ))}
            </ul>
          )}
        </Card>
      )}

      <p className="text-sm text-fg-muted">Signed in as {viewer.email}</p>
    </>
  );
}
