import "server-only";
import { cache } from "react";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { apiWithSession } from "@/lib/api/server";
import { ACTING_PLAYER_COOKIE } from "@/lib/session";
import { stubIdentity } from "./stub";
import type { Viewer } from "./types";

export type { PlayerSummary, Role, Viewer } from "./types";

/**
 * The current User, their roles and Players, and the Player they're acting as.
 *
 * proxy.ts only checks that a session cookie exists; this is the real validity
 * check — a revoked or expired session gets a 401 and lands back on /login.
 * Any other failure throws, so the route's error boundary shows it.
 *
 * Wrapped in React `cache`, so the layout and the page share one API round
 * trip per request.
 */
export const getViewer = cache(async (): Promise<Viewer> => {
  const api = await apiWithSession();
  const { data: me, response } = await api.GET("/auth/me");
  if (response.status === 401) {
    redirect("/login");
  }
  if (!me) {
    throw new Error(`GET /auth/me failed with ${response.status}`);
  }

  const { roles, players } = stubIdentity(me.email);
  const chosen = (await cookies()).get(ACTING_PLAYER_COOKIE)?.value;
  const actingPlayer = players.find((player) => player.id === chosen) ?? players[0] ?? null;

  return { userId: me.userId, email: me.email, roles, players, actingPlayer };
});
