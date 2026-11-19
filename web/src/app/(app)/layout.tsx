import Link from "next/link";
import type { ReactNode } from "react";
import { BottomNav, type NavKey } from "@/components/shell/bottom-nav";
import { PlayerSwitcher } from "@/components/shell/player-switcher";
import { getViewer } from "@/lib/viewer";

// Every signed-in page is rendered per request: the session check and the
// acting Player are request-time facts, never a build-time snapshot.
export const dynamic = "force-dynamic";

/**
 * The signed-in app shell (ADR-0014): header with the "Playing as" switcher,
 * page content, and bottom navigation. Manage and Admin entries appear only
 * for Users holding those roles — the pages themselves check again.
 */
export default async function AppLayout({ children }: { children: ReactNode }) {
  const viewer = await getViewer();

  const nav: NavKey[] = ["home", "leagues", "profiles"];
  if (viewer.roles.includes("Manager")) nav.push("manage");
  if (viewer.roles.includes("Admin")) nav.push("admin");

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="sticky top-0 z-10 border-b border-border bg-surface">
        <div className="mx-auto flex min-h-14 max-w-md items-center justify-between gap-3 px-4">
          <span className="text-lg font-bold">Hits & Giggles</span>
          <PlayerSwitcher viewer={viewer} />
        </div>
      </header>
      {/* Bottom padding clears the fixed navigation bar. */}
      <main className="mx-auto flex w-full max-w-md flex-1 flex-col gap-4 px-4 pt-4 pb-24">
        {children}
        <Link href="/privacy" className="mt-auto pt-4 text-center text-sm text-fg-muted underline">
          Privacy notice
        </Link>
      </main>
      <BottomNav keys={nav} />
    </div>
  );
}
