"use server";

import { revalidatePath } from "next/cache";
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

/** Where a form sits, so its action can send a refusal back to the same page. */
type NameFormPage = "/" | "/profiles";

function nameFormPage(formData: FormData): NameFormPage {
  return formData.get("from") === "/profiles" ? "/profiles" : "/";
}

/**
 * Add a Player to this login. The first one becomes the Acting Player by being
 * the only one; later ones don't take over.
 */
export async function createPlayer(formData: FormData) {
  const page = nameFormPage(formData);
  const name = formData.get("name");
  if (typeof name !== "string") {
    redirect(`${page}?error=name`);
  }

  const api = await apiWithSession();
  const { response } = await api.POST("/players", { body: { name } });
  if (response.status === 400) {
    redirect(`${page}?error=name`);
  }
  if (!response.ok) {
    throw new Error(`POST /players failed with ${response.status}`);
  }
  // The header's "Playing as" lives in the layout, so every page is stale.
  revalidatePath("/", "layout");
  redirect(page);
}

/** Change the Name of a Player this login holds. */
export async function renamePlayer(formData: FormData) {
  const playerId = formData.get("playerId");
  const name = formData.get("name");
  if (typeof playerId !== "string" || typeof name !== "string") {
    redirect("/profiles?error=name");
  }

  const api = await apiWithSession();
  const { response } = await api.PUT("/players/{playerId}", {
    params: { path: { playerId } },
    body: { name },
  });
  if (response.status === 400) {
    redirect("/profiles?error=name");
  }
  if (!response.ok) {
    throw new Error(`PUT /players/${playerId} failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
  redirect("/profiles");
}

/** The Acting Player joins a Club. Pressing twice joins once: the API's PUT is idempotent. */
export async function joinClub(formData: FormData) {
  const clubId = formData.get("clubId");
  const { actingPlayer } = await getViewer();
  if (typeof clubId !== "string" || !actingPlayer) {
    return;
  }

  const api = await apiWithSession();
  const { response } = await api.PUT("/clubs/{clubId}/members/{playerId}", {
    params: { path: { clubId, playerId: actingPlayer.id } },
  });
  if (!response.ok) {
    throw new Error(`Joining club ${clubId} failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}
