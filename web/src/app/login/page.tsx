import { redirect } from "next/navigation";
import { api } from "@/lib/api/client";

export const dynamic = "force-dynamic";

async function requestMagicLink(formData: FormData) {
  "use server";
  const email = formData.get("email");
  if (typeof email !== "string" || email.length === 0) {
    redirect("/login?error=invalid-email");
  }

  const { response } = await api.POST("/auth/magic-link", { body: { email } });
  redirect(response.ok ? `/login?sent=${encodeURIComponent(email)}` : "/login?error=invalid-email");
}

export default async function LoginPage({
  searchParams,
}: {
  searchParams: Promise<{ sent?: string; error?: string }>;
}) {
  const { sent, error } = await searchParams;

  return (
    <main className="mx-auto flex min-h-screen max-w-md flex-col justify-center gap-6 p-6">
      <h1 className="text-2xl font-bold">Social Tennis</h1>

      {sent ? (
        <section className="rounded-lg border p-4">
          <h2 className="font-semibold">Check your email</h2>
          <p className="mt-1 text-sm text-gray-600">
            A sign-in link is on its way to <strong>{sent}</strong>. It works
            once and expires shortly.
          </p>
          <p className="mt-2 text-xs text-gray-400">
            Local dev: the link is printed in the api container log.
          </p>
        </section>
      ) : (
        <form action={requestMagicLink} className="flex flex-col gap-3">
          {error === "invalid-email" && (
            <p role="alert" className="text-sm text-red-600">
              Please enter a valid email address.
            </p>
          )}
          {error === "invalid-link" && (
            <p role="alert" className="text-sm text-red-600">
              That sign-in link is invalid, already used, or expired. Request a
              fresh one below.
            </p>
          )}
          <label className="text-sm font-medium" htmlFor="email">
            Email address
          </label>
          <input
            id="email"
            name="email"
            type="email"
            required
            autoComplete="email"
            className="rounded-lg border p-3"
            placeholder="you@example.com"
          />
          <button type="submit" className="rounded-lg bg-black p-3 font-semibold text-white">
            Email me a sign-in link
          </button>
        </form>
      )}
    </main>
  );
}
