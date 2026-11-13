import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { EmptyState } from "@/components/ui/states";
import { getViewer } from "@/lib/viewer";

export const metadata: Metadata = { title: "Admin" };

/** Club and roles (#18). Admins only — refused here as well as hidden from the nav. */
export default async function AdminPage() {
  const viewer = await getViewer();
  if (!viewer.roles.includes("Admin")) {
    notFound();
  }

  return (
    <>
      <h1 className="text-2xl font-bold">Admin</h1>
      <EmptyState title="Club and roles" description="Club setup and role management arrive with #18." />
    </>
  );
}
