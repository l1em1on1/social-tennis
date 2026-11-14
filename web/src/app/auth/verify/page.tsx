import type { Metadata } from "next";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { Card } from "@/components/ui/card";
import { SubmitButton } from "@/components/ui/submit-button";
import { api } from "@/lib/api/client";
import { SESSION_COOKIE, secureCookies } from "@/lib/session";

export const dynamic = "force-dynamic";

export const metadata: Metadata = {
  title: "Sign in",
  // The address of this page is the credential until the button is pressed.
  referrer: "no-referrer",
  robots: { index: false },
};

/**
 * Redeems the raw token at the API and swaps it for an opaque session
 * credential in an HttpOnly cookie. Invalid, used, or expired links bounce
 * back to /login with a clear message.
 */
async function signIn(formData: FormData) {
  "use server";
  const token = formData.get("token");
  if (typeof token !== "string" || token.length === 0) {
    redirect("/login?error=invalid-link");
  }

  const { data, response } = await api.POST("/auth/sessions", { body: { token } });
  if (!response.ok || !data) {
    redirect("/login?error=invalid-link");
  }

  (await cookies()).set(SESSION_COOKIE, data.token, {
    httpOnly: true,
    sameSite: "lax",
    secure: secureCookies(),
    path: "/",
    expires: new Date(data.expiresAt),
  });
  redirect("/");
}

/**
 * Where the emailed magic link lands. Opening it redeems nothing: mail
 * scanners and link previewers follow GETs, and would otherwise use up the
 * single-use token before its owner clicks (#19). The token is redeemed only
 * by the button's POST.
 */
export default async function VerifyPage({ searchParams }: PageProps<"/auth/verify">) {
  const { token } = await searchParams;
  if (typeof token !== "string" || token.length === 0) {
    redirect("/login?error=invalid-link");
  }

  return (
    <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-6 px-4 py-8">
      <h1 className="text-3xl font-bold">Hits & Giggles</h1>
      <Card title="Finish signing in">
        <p className="text-fg-muted">Press the button to sign in on this device.</p>
        <form action={signIn} className="mt-4">
          <input type="hidden" name="token" value={token} />
          <SubmitButton fullWidth pendingLabel="Signing in…">
            Sign in
          </SubmitButton>
        </form>
      </Card>
    </main>
  );
}
