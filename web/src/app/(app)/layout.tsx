import Link from "next/link";
import type { ReactNode } from "react";
import { BallMark } from "@/components/shell/ball-mark";
import { BottomNav, type NavKey } from "@/components/shell/bottom-nav";
import { PlayerSwitcher } from "@/components/shell/player-switcher";
import { getViewer } from "@/lib/viewer";

// Every signed-in page is rendered per request: the session check and the
// acting Player are request-time facts, never a build-time snapshot.
export const dynamic = "force-dynamic";

/**
 * The signed-in app shell (ADR-0014): header with the "Playing as" switcher,
 * the main navigation, and the page. The navigation is in the header, first in
 * reading order: on a phone it is the bar of tabs at the bottom, and from the
 * `md` breakpoint a top bar in the header (#225). Content is a phone's width
 * below `md` and up to `max-w-5xl` above it, where pages lay out in columns.
 * Manage and Admin entries appear only for Users holding those roles — the
 * pages themselves check again.
 */
export default async function AppLayout({ children }: { children: ReactNode }) {
  const viewer = await getViewer();

  const nav: NavKey[] = ["home", "leagues", "profiles"];
  if (viewer.roles.includes("Manager")) nav.push("manage");
  if (viewer.roles.includes("Admin")) nav.push("admin");

  return (
    <div className="flex min-h-dvh flex-col">
      {/* Ruled in terracotta, with the ball mark (#226). */}
      <header className="sticky top-0 z-10 border-b-4 border-accent bg-surface">
        <div className="mx-auto flex min-h-14 max-w-md items-center justify-between gap-3 px-4 md:max-w-5xl">
          <span className="flex shrink-0 items-center gap-2 font-display text-lg font-bold text-accent">
            <BallMark />
            Hits & Giggles
          </span>
          <BottomNav keys={nav} />
          <PlayerSwitcher viewer={viewer} />
        </div>
      </header>
      {/* Below md, bottom padding clears the fixed bar of tabs. */}
      <main className="mx-auto flex w-full max-w-md flex-1 flex-col gap-4 px-4 pt-4 pb-24 md:max-w-5xl md:pt-6 md:pb-10">
        {children}
        <Link href="/privacy" className="mt-auto pt-4 text-center text-sm text-fg-muted underline">
          Privacy notice
        </Link>
      </main>
    </div>
  );
}
