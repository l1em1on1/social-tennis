import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { AvailabilityForm } from "@/components/availability-form";
import { PageColumn } from "@/components/shell/page-column";
import { ButtonLink } from "@/components/ui/button";
import { ErrorState } from "@/components/ui/states";
import { apiWithSession } from "@/lib/api/server";
import { isId } from "@/lib/ids";
import { formatDates } from "@/lib/league";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "Free days" };

/**
 * The Acting Player's free days in a League's current Stage (#9): a calendar
 * to tick, so each of their Games can show the days that suit everyone in it.
 * Only theirs: nobody else sees this list. A Player not in the League, or a
 * League not started or ended, gets a 404.
 */
export default async function AvailabilityPage({ params, searchParams }: PageProps<"/leagues/[leagueId]/availability">) {
  const [viewer, api, { leagueId }, { saved }] = await Promise.all([getViewer(), apiWithSession(), params, searchParams]);
  const acting = viewer.actingPlayer;
  if (!isId(leagueId) || !acting) {
    notFound();
  }

  const { data, response } = await api.GET("/leagues/{leagueId}/players/{playerId}/availability", {
    params: { path: { leagueId, playerId: acting.id } },
  });
  if (response.status === 404) {
    notFound();
  }
  if (!data) {
    throw new Error(`GET /leagues/${leagueId}/players/${acting.id}/availability failed with ${response.status}`);
  }

  const { stage } = data;
  return (
    <PageColumn>
      <div className="flex flex-col gap-1">
        <h1 className="text-4xl leading-none font-black break-words">Free days</h1>
        <p className="text-fg-muted">
          {acting.name} · Stage {Number(stage.number)}, {formatDates(stage.startDate, stage.endDate)}
        </p>
      </div>
      <p className="text-sm">
        Tick the days you could play. Each of your games then shows the days that suit everyone in it. Only you see this
        list.
      </p>
      {saved === "1" && (
        <p role="status" className="rounded-md border border-success bg-success-muted px-4 py-3 text-sm">
          Saved. Your games show the days that suit everyone.
        </p>
      )}
      {saved === "refused" && (
        <ErrorState title="That didn't save" description="The Stage changed meanwhile. These are the days it takes now." />
      )}
      <AvailabilityForm
        leagueId={leagueId}
        playerId={acting.id}
        firstDay={stage.startDate}
        lastDay={data.lastDay}
        dates={data.days.filter((day) => day.free).map((day) => day.date)}
      />
      <div>
        <ButtonLink href={`/leagues/${leagueId}`} variant="secondary">
          Back to the ladder
        </ButtonLink>
      </div>
    </PageColumn>
  );
}
