"use client";

import { Button } from "@/components/ui/button";
import { ErrorState } from "@/components/ui/states";

/** Error boundary for signed-in pages: say what happened, offer a retry. */
export default function AppError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <ErrorState
      title="Something went wrong"
      description="This page couldn't load. It's usually the server being briefly unreachable."
      action={
        <Button variant="secondary" onClick={reset}>
          Try again
        </Button>
      }
    />
  );
}
