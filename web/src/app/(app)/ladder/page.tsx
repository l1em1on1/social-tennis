import type { Metadata } from "next";
import { EmptyState } from "@/components/ui/states";

export const metadata: Metadata = { title: "Ladder" };

/** League ladder — filled in by #7 (Divisional League starts). */
export default function LadderPage() {
  return (
    <>
      <h1 className="text-2xl font-bold">Ladder</h1>
      <EmptyState
        title="No ladder yet"
        description="The ladder appears here once a Manager starts a league you're in."
      />
    </>
  );
}
