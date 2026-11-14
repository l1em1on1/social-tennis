"use client";

import { useLinkStatus } from "next/link";
import type { ReactNode } from "react";
import { Spinner } from "./spinner";

/**
 * Placed inside a `<Link>`: shows a spinner from the tap until the next page
 * takes over (`useLinkStatus`), and `children` otherwise. On a cold server
 * that gap is seconds, and without this the tap looks ignored.
 */
export function LinkPending({ children, className }: { children?: ReactNode; className?: string }) {
  const { pending } = useLinkStatus();
  return pending ? <Spinner className={className} /> : children;
}
