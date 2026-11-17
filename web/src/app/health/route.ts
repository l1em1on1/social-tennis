import { mockApi } from "@/lib/mock-mode";

// Every request asks the API afresh; an answer from a cache would prove nothing.
export const dynamic = "force-dynamic";

/** A health answer is about this moment: nothing on the way may keep it. */
const NO_STORE = { "Cache-Control": "no-store" };

/** Long enough for the API's cold start: both apps scale to zero (ADR-0019). */
const API_TIMEOUT_MS = 25_000;

/**
 * Says whether web is up and can reach the API (#111). The deploy check calls
 * it, so a deploy is green only when the browser-facing app can talk to the
 * API over the address and protocol production uses. It asks the API's
 * `/health`, which runs no checks, so it wakes the API but never the database.
 * The liveness probe stays on `/login`, which wakes nothing.
 *
 * The answer is 200 or 503 and says nothing else: why the API couldn't be
 * reached is in web's log, not in a public response.
 */
export async function GET() {
  if (mockApi) {
    return Response.json({ status: "ok" }, { headers: NO_STORE });
  }

  try {
    const response = await fetch(new URL("/health", process.env.API_URL), {
      cache: "no-store",
      signal: AbortSignal.timeout(API_TIMEOUT_MS),
    });
    if (response.ok) {
      return Response.json({ status: "ok" }, { headers: NO_STORE });
    }
    console.error(`Health: the API answered ${response.status}.`);
  } catch (error) {
    console.error("Health: the API could not be reached.", error);
  }
  return Response.json({ status: "unavailable" }, { status: 503, headers: NO_STORE });
}
