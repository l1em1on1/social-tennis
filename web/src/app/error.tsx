"use client";

import { Button } from "@/components/ui/button";
import { ErrorState } from "@/components/ui/states";

/**
 * Root error boundary. A segment's error.tsx sits *inside* its layout, so a
 * throw from the (app) layout itself — `getViewer()` when /auth/me fails —
 * skips `(app)/error.tsx` and lands here. No shell: the layout that draws it
 * is what failed.
 */
export default function RootError({ retry }: { error: Error & { digest?: string }; retry: () => void }) {
  return (
    <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-4 px-4 py-8">
      <h1 className="text-2xl font-bold">Social Tennis</h1>
      <ErrorState
        title="Something went wrong"
        description="The app couldn't load. It's usually the server being briefly unreachable."
        action={
          <Button variant="secondary" onClick={() => retry()}>
            Try again
          </Button>
        }
      />
    </main>
  );
}
