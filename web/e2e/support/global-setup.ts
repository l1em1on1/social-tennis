import { takeNewestMagicLink } from "./auth";

/**
 * Compose already holds the runner back until both web builds and the api
 * report healthy. This repeats the wait for runs that bypass that: a target
 * overridden with E2E_*_URL, or `run --no-deps`.
 */
const targets = [
  process.env.E2E_MOCK_URL ?? "http://web-mock:3000",
  process.env.E2E_REAL_URL ?? "http://web-e2e:3000",
].map((origin) => `${origin}/login`);
targets.push(`${process.env.E2E_API_URL ?? "http://api:8080"}/health/ready`);

async function waitFor(url: string, timeoutMs = 180_000) {
  const deadline = Date.now() + timeoutMs;
  let last = "no response";
  while (Date.now() < deadline) {
    try {
      const response = await fetch(url);
      if (response.ok) return;
      last = `HTTP ${response.status}`;
    } catch (error) {
      last = error instanceof Error ? error.message : String(error);
    }
    await new Promise((resolve) => setTimeout(resolve, 1000));
  }
  throw new Error(`${url} not ready after ${timeoutMs / 1000}s (${last})`);
}

/**
 * The real API starts with no Club (an Admin creates it, #18), and most specs
 * need one to ask to join. So the suite's Admin creates it, through the API as
 * a person would: sign in as the address the compose file lists in
 * Auth__AdminEmails, then POST /clubs. Same name as the mocked API's Club.
 *
 * Skipped when one exists already (the database outlives a run), and when
 * there is no database to sign in through (a mock-only run).
 */
async function ensureAClub() {
  if (!process.env.DATABASE_URL) return;

  const api = process.env.E2E_API_URL ?? "http://api:8080";
  const email = process.env.E2E_ADMIN_EMAIL ?? "admin@example.com";
  const json = { "Content-Type": "application/json" };

  const requested = await fetch(`${api}/auth/magic-link`, { method: "POST", headers: json, body: JSON.stringify({ email }) });
  if (!requested.ok) throw new Error(`Requesting the Admin's sign-in link failed: HTTP ${requested.status}`);
  const token = await takeNewestMagicLink(email);
  const redeemed = await fetch(`${api}/auth/sessions`, { method: "POST", headers: json, body: JSON.stringify({ token }) });
  if (!redeemed.ok) throw new Error(`Signing in as the Admin failed: HTTP ${redeemed.status}`);
  const session = (await redeemed.json()) as { token: string };
  const authorized = { ...json, Authorization: `Bearer ${session.token}` };

  const listed = await fetch(`${api}/clubs`, { headers: authorized });
  const { clubs } = (await listed.json()) as { clubs: unknown[] };
  if (clubs.length > 0) return;

  const created = await fetch(`${api}/clubs`, {
    method: "POST",
    headers: authorized,
    body: JSON.stringify({ name: "Riverside Tennis Club" }),
  });
  if (!created.ok) {
    throw new Error(
      `Creating the suite's Club failed: HTTP ${created.status}. Is ${email} in the api's Auth__AdminEmails?`,
    );
  }
}

export default async function globalSetup() {
  await Promise.all(targets.map((url) => waitFor(url)));
  await ensureAClub();
}
