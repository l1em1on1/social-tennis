"use client";

import { useTransition } from "react";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { ErrorState } from "@/components/ui/states";

/**
 * Root error boundary. A segment's error.tsx sits *inside* its layout, so a
 * throw from the (app) layout itself — `getViewer()` when /auth/me fails —
 * skips `(app)/error.tsx` and lands here. No shell: the layout that draws it
 * is what failed.
 */
export default function RootError({ retry }: { error: Error & { digest?: string }; retry: () => void }) {
  // The retry re-fetches the page from the server; show that it is happening.
  const [retrying, startRetry] = useTransition();

  return (
    <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-4 px-4 py-8">
      <h1 className="text-4xl leading-none font-black">Hits & Giggles</h1>
      <ErrorState
        title="Something went wrong"
        description="The app couldn't load. It's usually the server being briefly unreachable."
        action={
          <Button variant="secondary" disabled={retrying} aria-busy={retrying || undefined} onClick={() => startRetry(() => retry())}>
            {retrying && <Spinner />}
            Try again
          </Button>
        }
      />
    </main>
  );
}
