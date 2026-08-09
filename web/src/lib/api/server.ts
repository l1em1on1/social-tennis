import "server-only";
import { cookies } from "next/headers";
import createClient from "openapi-fetch";
import type { paths } from "./schema";
import { SESSION_COOKIE } from "@/lib/session";

/**
 * API client carrying the current visitor's session (if any) as a Bearer
 * token. The opaque session credential lives in an HttpOnly cookie the
 * browser can't read; only this server-side seam ever attaches it to a
 * request (ADR-0001 — tokens stay server-side).
 */
export async function apiWithSession() {
  const session = (await cookies()).get(SESSION_COOKIE)?.value;
  return createClient<paths>({
    baseUrl: process.env.API_URL,
    headers: session ? { Authorization: `Bearer ${session}` } : undefined,
  });
}
