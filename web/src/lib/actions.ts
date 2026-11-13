"use server";

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { apiWithSession } from "@/lib/api/server";
import { ACTING_PLAYER_COOKIE, SESSION_COOKIE, secureCookies } from "@/lib/session";
import { getViewer } from "@/lib/viewer";

/**
 * Act as another of the User's Players. The id is checked against the Players
 * the User actually holds — a forged form value is ignored, not stored.
 */
export async function switchActingPlayer(formData: FormData) {
  const playerId = formData.get("playerId");
  const viewer = await getViewer();
  if (typeof playerId !== "string" || !viewer.players.some((player) => player.id === playerId)) {
    return;
  }

  (await cookies()).set(ACTING_PLAYER_COOKIE, playerId, {
    httpOnly: true,
    sameSite: "lax",
    secure: secureCookies(),
    path: "/",
  });
}

export async function logout() {
  const api = await apiWithSession();
  // Revoke server-side first — the credential dies even if a cookie copy
  // survives somewhere — then drop the cookies.
  await api.DELETE("/auth/sessions/current");
  const jar = await cookies();
  jar.delete(SESSION_COOKIE);
  jar.delete(ACTING_PLAYER_COOKIE);
  redirect("/login");
}
