"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";
import { cx } from "@/lib/cx";

export type NavKey = "home" | "ladder" | "profiles" | "manage" | "admin";

const items: Record<NavKey, { href: string; label: string; icon: ReactNode }> = {
  home: { href: "/", label: "Home", icon: <path d="M3 10.5 12 3l9 7.5V21h-6v-6H9v6H3z" /> },
  ladder: { href: "/ladder", label: "Ladder", icon: <path d="M7 3v18M17 3v18M7 7h10M7 12h10M7 17h10" /> },
  profiles: {
    href: "/profiles",
    label: "Profiles",
    icon: <path d="M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM4 21a8 8 0 0 1 16 0" />,
  },
  manage: { href: "/manage", label: "Manage", icon: <path d="M4 6h16M4 12h16M4 18h10M18 16l2 2 3-4" /> },
  admin: {
    href: "/admin",
    label: "Admin",
    icon: <path d="M12 3 4 6v6c0 4.5 3.4 8.2 8 9 4.6-.8 8-4.5 8-9V6z" />,
  },
};

function isActive(pathname: string, href: string) {
  return href === "/" ? pathname === "/" : pathname === href || pathname.startsWith(`${href}/`);
}

/**
 * Mobile bottom navigation. The server decides which entries a User sees
 * (Manage and Admin by role); this client component only marks the active one.
 */
export function BottomNav({ keys }: { keys: NavKey[] }) {
  const pathname = usePathname();

  return (
    <nav
      aria-label="Main"
      className="fixed inset-x-0 bottom-0 z-10 border-t border-border bg-surface pb-[env(safe-area-inset-bottom)]"
    >
      <ul className="mx-auto flex max-w-md">
        {keys.map((key) => {
          const { href, label, icon } = items[key];
          const active = isActive(pathname, href);
          return (
            <li key={key} className="flex-1">
              <Link
                href={href}
                aria-current={active ? "page" : undefined}
                className={cx(
                  "flex min-h-14 flex-col items-center justify-center gap-0.5 text-xs font-medium",
                  active ? "text-accent" : "text-fg-muted hover:text-fg",
                )}
              >
                <svg
                  aria-hidden="true"
                  viewBox="0 0 24 24"
                  className="size-6 fill-none stroke-current stroke-2 [stroke-linecap:round] [stroke-linejoin:round]"
                >
                  {icon}
                </svg>
                {label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
