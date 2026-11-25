import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { LeagueSetupForm } from "@/components/league-setup-form";
import { apiWithSession } from "@/lib/api/server";
import { getViewer } from "@/lib/viewer";
import { PageColumn } from "@/components/shell/page-column";

export const metadata: Metadata = { title: "New league" };

/** Today as a calendar date where the club is (ADR-0016): the first club is in Europe/London. */
function todayAtTheClub(): string {
  // en-CA formats as YYYY-MM-DD, what a date input takes.
  return new Date().toLocaleDateString("en-CA", { timeZone: "Europe/London" });
}

/** League setup (wireframe M2), for the Club named in `?club=`. */
export default async function NewLeaguePage({ searchParams }: PageProps<"/manage/leagues/new">) {
  const [viewer, api, { club: clubId }] = await Promise.all([getViewer(), apiWithSession(), searchParams]);
  if (!viewer.manages) {
    notFound();
  }

  const { data: clubList } = await api.GET("/clubs");
  if (!clubList) {
    throw new Error("GET /clubs failed");
  }
  // Someone who only runs a League there can't create another (#220): the API would refuse the form.
  const club = clubList.clubs.find((candidate) => candidate.id === clubId);
  if (!club?.canCreateLeagues) {
    notFound();
  }

  return (
    <PageColumn>
      <div>
        <h1 className="text-4xl leading-none font-black">New league</h1>
        <p className="text-fg-muted">{club.name}</p>
      </div>
      <LeagueSetupForm clubId={club.id} today={todayAtTheClub()} />
    </PageColumn>
  );
}
