"use client";

import { useTransition } from "react";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { ErrorState } from "@/components/ui/states";

/**
 * Error boundary for signed-in pages: say what happened, offer a retry.
 * `retry()`, not `reset()`: reset only clears the boundary state, so a failed
 * Server Component (say, a transient /clubs fetch) would fail again without
 * re-fetching (Next.js 16 error.js docs).
 */
export default function AppError({ retry }: { error: Error & { digest?: string }; retry: () => void }) {
  // The retry re-fetches the page from the server; show that it is happening.
  const [retrying, startRetry] = useTransition();

  return (
    <ErrorState
      title="Something went wrong"
      description="This page couldn't load. It's usually the server being briefly unreachable."
      action={
        <Button variant="secondary" disabled={retrying} aria-busy={retrying || undefined} onClick={() => startRetry(() => retry())}>
          {retrying && <Spinner />}
          Try again
        </Button>
      }
    />
  );
}
