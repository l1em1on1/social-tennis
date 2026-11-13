import { ButtonLink } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/states";

export default function NotFound() {
  return (
    <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-4 px-4 py-8">
      <h1 className="text-2xl font-bold">Page not found</h1>
      <EmptyState
        title="Nothing here"
        description="This page doesn't exist, or it's not available to you."
        action={<ButtonLink href="/">Go home</ButtonLink>}
      />
    </main>
  );
}
