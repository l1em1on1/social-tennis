"use client";

import { Button } from "@/components/ui/button";
import { ErrorState } from "@/components/ui/states";

/**
 * Error boundary for signed-in pages: say what happened, offer a retry.
 * `retry()`, not `reset()`: reset only clears the boundary state, so a failed
 * Server Component (say, a transient /clubs fetch) would fail again without
 * re-fetching (Next.js 16 error.js docs).
 */
export default function AppError({ retry }: { error: Error & { digest?: string }; retry: () => void }) {
  return (
    <ErrorState
      title="Something went wrong"
      description="This page couldn't load. It's usually the server being briefly unreachable."
      action={
        <Button variant="secondary" onClick={() => retry()}>
          Try again
        </Button>
      }
    />
  );
}
