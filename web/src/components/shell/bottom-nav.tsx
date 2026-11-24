"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";
import { LinkPending } from "@/components/ui/link-pending";
import { cx } from "@/lib/cx";

export type NavKey = "home" | "leagues" | "profiles" | "manage" | "admin";

const items: Record<NavKey, { href: string; label: string; icon: ReactNode }> = {
  home: { href: "/", label: "Home", icon: <path d="M3 10.5 12 3l9 7.5V21h-6v-6H9v6H3z" /> },
  leagues: { href: "/leagues", label: "Leagues", icon: <path d="M7 3v18M17 3v18M7 7h10M7 12h10M7 17h10" /> },
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
 * The main navigation. On a phone, a bar of tabs fixed to the bottom of the
 * screen; from the `md` breakpoint, a top bar in the header (#225), where the
 * shell renders it either way. The server decides which entries a User sees
 * (Manage and Admin by role); this client component only marks the active one.
 */
export function BottomNav({ keys }: { keys: NavKey[] }) {
  const pathname = usePathname();

  return (
    <nav
      aria-label="Main"
      className={cx(
        "fixed inset-x-0 bottom-0 z-10 border-t border-border bg-surface pb-[env(safe-area-inset-bottom)]",
        "md:static md:z-auto md:flex-1 md:border-t-0 md:bg-transparent md:pb-0",
      )}
    >
      <ul className="mx-auto flex max-w-md md:max-w-none md:justify-center md:gap-1">
        {keys.map((key) => {
          const { href, label, icon } = items[key];
          const active = isActive(pathname, href);
          return (
            <li key={key} className="flex-1 md:flex-none">
              <Link
                href={href}
                aria-current={active ? "page" : undefined}
                className={cx(
                  "flex min-h-14 flex-col items-center justify-center gap-0.5 text-xs font-medium",
                  "md:min-h-11 md:flex-row md:gap-2 md:rounded-xl md:px-3 md:text-sm",
                  active ? "text-accent md:bg-accent-muted" : "text-fg-muted hover:text-fg md:hover:bg-surface-muted",
                )}
              >
                {/* The icon turns into a spinner from the tap until the page arrives. */}
                <LinkPending className="size-6 md:size-5">
                  <svg
                    aria-hidden="true"
                    viewBox="0 0 24 24"
                    className="size-6 fill-none stroke-current stroke-2 [stroke-linecap:round] [stroke-linejoin:round] md:size-5"
                  >
                    {icon}
                  </svg>
                </LinkPending>
                {label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
