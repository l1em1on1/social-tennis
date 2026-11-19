import type { Metadata } from "next";
import { headers } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { buttonClasses } from "@/components/ui/button";
import { SubmitButton } from "@/components/ui/submit-button";
import { Card } from "@/components/ui/card";
import { Field, Input } from "@/components/ui/field";
import { ErrorState } from "@/components/ui/states";
import { api } from "@/lib/api/client";
import { CLIENT_ADDRESS_HEADER, clientAddress } from "@/lib/client-address";
import { mockApi } from "@/lib/mock-mode";

export const dynamic = "force-dynamic";

export const metadata: Metadata = { title: "Log in" };

async function requestMagicLink(formData: FormData) {
  "use server";
  const email = formData.get("email");
  if (typeof email !== "string" || email.length === 0) {
    redirect("/login?error=invalid-email");
  }

  // The API limits requests per client (#108); it can't see the browser itself.
  const address = clientAddress((await headers()).get("x-forwarded-for"));
  const { response } = await api.POST("/auth/magic-link", {
    body: { email },
    headers: address ? { [CLIENT_ADDRESS_HEADER]: address } : undefined,
  });
  if (response.status === 429) {
    redirect("/login?error=try-later");
  }
  if (response.status === 503) {
    redirect("/login?error=send-failed");
  }
  if (response.ok) {
    redirect(`/login?sent=${encodeURIComponent(email)}`);
  }
  // 400 is the API rejecting the address. Anything else is our fault, and
  // must not be blamed on what the person typed.
  redirect(response.status === 400 ? "/login?error=invalid-email" : "/login?error=unexpected");
}

const errors: Record<string, { title: string; description: string }> = {
  "invalid-email": { title: "That email didn't work", description: "Please enter a valid email address." },
  "try-later": {
    title: "Too many sign-in requests",
    description: "Please wait a minute, then try again.",
  },
  "send-failed": {
    title: "We couldn't send the email",
    description: "Nothing was sent. Please try again in a moment.",
  },
  unexpected: {
    title: "Something went wrong",
    description: "That's a problem on our side. Please try again in a moment.",
  },
  "invalid-link": {
    title: "That sign-in link has expired",
    description: "It's invalid, already used, or too old. Request a fresh one below.",
  },
};

/**
 * `sent` arrives in the URL, so anyone can link to /login?sent=<any text>. It
 * is shown back only if it looks like an email address (#53): otherwise the
 * page would display a stranger's chosen words on this site's address.
 */
const looksLikeEmail = (value: unknown): value is string =>
  typeof value === "string" && value.length <= 320 && /^[^\s@<>]+@[^\s@<>]+\.[^\s@<>]+$/.test(value);

export default async function LoginPage({ searchParams }: PageProps<"/login">) {
  const { sent, error } = await searchParams;
  const sentTo = looksLikeEmail(sent) ? sent : undefined;
  const problem = typeof error === "string" ? errors[error] : undefined;

  return (
    <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-6 px-4 py-8">
      <h1 className="text-3xl font-bold">Hits & Giggles</h1>

      {sentTo ? (
        <Card title="Check your email">
          <p className="text-fg-muted">
            A sign-in link is on its way to <strong className="text-fg">{sentTo}</strong>. It works once and expires
            shortly.
          </p>
          {mockApi ? (
            // Mock mode has no inbox: the mocked API accepts the email itself as the link token.
            <a
              href={`/auth/verify?token=${encodeURIComponent(sentTo)}`}
              className={`${buttonClasses("primary", true)} mt-4`}
            >
              Continue (mock API)
            </a>
          ) : (
            // `next dev` only: there the API logs the email instead of sending it.
            process.env.NODE_ENV !== "production" && (
              <p className="mt-2 text-sm text-fg-muted">Local dev: the link is printed in the api container log.</p>
            )
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
          <SubmitButton fullWidth pendingLabel="Sending…">
            Email me a sign-in link
          </SubmitButton>
        </form>
      )}
      <Link href="/privacy" className="text-center text-sm text-fg-muted underline">
        Privacy notice
      </Link>
    </main>
  );
}
