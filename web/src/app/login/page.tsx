import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { Button, buttonClasses } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Field, Input } from "@/components/ui/field";
import { ErrorState } from "@/components/ui/states";
import { api } from "@/lib/api/client";
import { mockApi } from "@/lib/mock-mode";

export const dynamic = "force-dynamic";

export const metadata: Metadata = { title: "Log in" };

async function requestMagicLink(formData: FormData) {
  "use server";
  const email = formData.get("email");
  if (typeof email !== "string" || email.length === 0) {
    redirect("/login?error=invalid-email");
  }

  const { response } = await api.POST("/auth/magic-link", { body: { email } });
  redirect(response.ok ? `/login?sent=${encodeURIComponent(email)}` : "/login?error=invalid-email");
}

const errors: Record<string, { title: string; description: string }> = {
  "invalid-email": { title: "That email didn't work", description: "Please enter a valid email address." },
  "invalid-link": {
    title: "That sign-in link has expired",
    description: "It's invalid, already used, or too old. Request a fresh one below.",
  },
};

export default async function LoginPage({ searchParams }: PageProps<"/login">) {
  const { sent, error } = await searchParams;
  const sentTo = typeof sent === "string" ? sent : undefined;
  const problem = typeof error === "string" ? errors[error] : undefined;

  return (
    <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-6 px-4 py-8">
      <h1 className="text-3xl font-bold">Social Tennis</h1>

      {sentTo ? (
        <Card title="Check your email">
          <p className="text-fg-muted">
            A sign-in link is on its way to <strong className="text-fg">{sentTo}</strong>. It works once and expires
            shortly.
          </p>
          {mockApi ? (
            // Mock mode has no inbox: the mocked API accepts the email itself as the link token.
            // A plain <a>, not next/link: the target is a route handler, which the
            // client router would prefetch — redeeming the "emailed" link early.
            <a
              href={`/auth/verify?token=${encodeURIComponent(sentTo)}`}
              className={`${buttonClasses("primary", true)} mt-4`}
            >
              Continue (mock API)
            </a>
          ) : (
            <p className="mt-2 text-sm text-fg-muted">Local dev: the link is printed in the api container log.</p>
          )}
        </Card>
      ) : (
        <form action={requestMagicLink} className="flex flex-col gap-4">
          {problem && <ErrorState title={problem.title} description={problem.description} />}
          <Field id="email" label="Email address" hint="We'll email you a link to sign in. No password needed.">
            {(control) => (
              <Input {...control} name="email" type="email" required autoComplete="email" placeholder="you@example.com" />
            )}
          </Field>
          <Button type="submit" fullWidth>
            Email me a sign-in link
          </Button>
        </form>
      )}
    </main>
  );
}
