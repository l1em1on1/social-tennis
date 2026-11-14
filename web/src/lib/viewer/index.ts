import "server-only";
import { cache } from "react";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { apiWithSession } from "@/lib/api/server";
import { ACTING_PLAYER_COOKIE } from "@/lib/session";
import type { Role, Viewer } from "./types";

export type { PlayerClub, PlayerSummary, Role, Viewer } from "./types";

/**
 * The current User, their roles and Players, and the Player they're acting as.
 * Roles decide what the shell shows; the API checks them again on every call.
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
  const [{ data: me, response }, { data: playerList, response: playersResponse }] = await Promise.all([
    api.GET("/auth/me"),
    api.GET("/players"),
  ]);
  if (response.status === 401 || playersResponse.status === 401) {
    redirect("/login");
  }
  if (!me) {
    throw new Error(`GET /auth/me failed with ${response.status}`);
  }
  if (!playerList) {
    throw new Error(`GET /players failed with ${playersResponse.status}`);
  }

  // The API names roles as strings; anything this build doesn't know is dropped.
  const roles = me.roles.filter((role): role is Role => role === "Manager" || role === "Admin");
  // Oldest first, so with no choice made the User acts as the first Player they added.
  const players = playerList.players;
  const chosen = (await cookies()).get(ACTING_PLAYER_COOKIE)?.value;
  const actingPlayer = players.find((player) => player.id === chosen) ?? players[0] ?? null;

  return { userId: me.userId, email: me.email, roles, players, actingPlayer };
});
