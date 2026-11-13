import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { EmptyState } from "@/components/ui/states";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "Manage" };

/**
 * Manager area — league setup (#5), starting the ladder (#7), the to-do queue
 * and month close land here. Hidden from the nav for non-Managers, and
 * refused here too, since a URL can be typed. The API stays the real gate.
 */
export default async function ManagePage() {
  const viewer = await getViewer();
  if (!viewer.roles.includes("Manager")) {
    notFound();
  }

  return (
    <>
      <h1 className="text-2xl font-bold">Manage</h1>
      <EmptyState title="Nothing to manage yet" description="League setup arrives here next." />
    </>
  );
}
