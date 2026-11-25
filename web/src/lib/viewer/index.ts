import "server-only";
import { cache } from "react";
import { cookies, headers } from "next/headers";
import { redirect } from "next/navigation";
import { apiWithSession } from "@/lib/api/server";
import { JOIN_CODE_HEADER, loginPath } from "@/lib/join";
import { ACTING_PLAYER_COOKIE } from "@/lib/session";
import type { Viewer } from "./types";

export type { PlayerClub, PlayerGender, PlayerSummary, Viewer } from "./types";

/**
 * The current User, their Players, and the Player they're acting as. Whether
 * they're an Admin or manage anything decides what the shell shows; the API
 * checks again on every call.
 *
 * proxy.ts only checks that a session cookie exists; this is the real validity
 * check — a revoked or expired session gets a 401 and lands back on /login,
 * with the code of the Club join link it was opening, if any (#222).
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
    redirect(loginPath((await headers()).get(JOIN_CODE_HEADER)));
  }
  if (!me) {
    throw new Error(`GET /auth/me failed with ${response.status}`);
  }
  if (!playerList) {
    throw new Error(`GET /players failed with ${playersResponse.status}`);
  }

  // Oldest first, so with no choice made the User acts as the first Player they added.
  // The generated type allows a level as text (how the API's schema describes an integer); it is always a number.
  const players = playerList.players.map((player) => ({
    ...player,
    level: player.level === null ? null : Number(player.level),
  }));
  const chosen = (await cookies()).get(ACTING_PLAYER_COOKIE)?.value;
  const actingPlayer = players.find((player) => player.id === chosen) ?? players[0] ?? null;

  return { userId: me.userId, email: me.email, admin: me.admin, manages: me.manages, managesClub: me.managesClub, players, actingPlayer };
});
