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

/**
 * A Player asks to join a Club. A Manager approves it, unless the User's email
 * was invited, in which case they are in at once. Pressing twice asks once:
 * the API's PUT is idempotent.
 *
 * The Player comes from the form, not the Acting Player cookie: the page may
 * have been rendered for a Player since switched away from in another tab, and
 * the request must be for the one the person saw. It is checked against the
 * Players this login holds, as `switchActingPlayer` does.
 */
export async function joinClub(formData: FormData) {
  const path = clubAndPlayer(formData);
  const viewer = await getViewer();
  if (!path || !viewer.players.some((player) => player.id === path.playerId)) {
    return;
  }

  const api = await apiWithSession();
  const { response } = await api.PUT("/clubs/{clubId}/members/{playerId}", { params: { path } });
  if (!response.ok) {
    throw new Error(`Joining club ${path.clubId} failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

function clubAndPlayer(formData: FormData): { clubId: string; playerId: string } | null {
  const clubId = formData.get("clubId");
  const playerId = formData.get("playerId");
  return typeof clubId === "string" && typeof playerId === "string" ? { clubId, playerId } : null;
}

/** A Manager approves a request to join. The API refuses anyone who isn't one. */
export async function approveClubMember(formData: FormData) {
  const path = clubAndPlayer(formData);
  if (!path) return;

  const api = await apiWithSession();
  const { response } = await api.PUT("/clubs/{clubId}/members/{playerId}/approval", { params: { path } });
  // 404: another Manager declined it meanwhile. The refreshed list shows that.
  if (!response.ok && response.status !== 404) {
    throw new Error(`Approving a club member failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

/** A Manager declines a request to join, or removes a member. */
export async function removeClubMember(formData: FormData) {
  const path = clubAndPlayer(formData);
  if (!path) return;

  const api = await apiWithSession();
  const { response } = await api.DELETE("/clubs/{clubId}/members/{playerId}", { params: { path } });
  if (!response.ok) {
    throw new Error(`Removing a club member failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

/** A Manager lets an email into a Club ahead of time. */
export async function inviteToClub(formData: FormData) {
  const clubId = formData.get("clubId");
  const email = formData.get("email");
  if (typeof clubId !== "string" || typeof email !== "string") {
    redirect("/manage?error=email");
  }

  const api = await apiWithSession();
  const { response } = await api.POST("/clubs/{clubId}/invites", { params: { path: { clubId } }, body: { email } });
  if (response.status === 400) {
    redirect("/manage?error=email");
  }
  if (!response.ok) {
    throw new Error(`Inviting to club ${clubId} failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
  redirect(`/manage?invited=${encodeURIComponent(email.trim())}`);
}

/** A Manager takes back an invite. Players it already let in stay members. */
export async function withdrawClubInvite(formData: FormData) {
  const clubId = formData.get("clubId");
  const inviteId = formData.get("inviteId");
  if (typeof clubId !== "string" || typeof inviteId !== "string") return;

  const api = await apiWithSession();
  const { response } = await api.DELETE("/clubs/{clubId}/invites/{inviteId}", {
    params: { path: { clubId, inviteId } },
  });
  if (!response.ok) {
    throw new Error(`Withdrawing a club invite failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

/**
 * One of this login's Players leaves a Club, or takes back a request that is
 * still waiting. The Player is checked against the ones this login holds; the
 * API checks again.
 */
export async function leaveClub(formData: FormData) {
  const path = clubAndPlayer(formData);
  const viewer = await getViewer();
  if (!path || !viewer.players.some((player) => player.id === path.playerId)) {
    return;
  }

  const api = await apiWithSession();
  const { response } = await api.DELETE("/players/{playerId}/clubs/{clubId}", { params: { path } });
  if (!response.ok) {
    throw new Error(`Leaving club ${path.clubId} failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

/** An Admin creates a Club. */
export async function createClub(formData: FormData) {
  const name = formData.get("name");
  if (typeof name !== "string") {
    redirect("/admin?error=name");
  }

  const api = await apiWithSession();
  const { response } = await api.POST("/clubs", { body: { name } });
  if (response.status === 400) {
    redirect("/admin?error=name");
  }
  if (!response.ok) {
    throw new Error(`POST /clubs failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
  redirect("/admin");
}

/** An Admin renames a Club. */
export async function renameClub(formData: FormData) {
  const clubId = formData.get("clubId");
  const name = formData.get("name");
  if (typeof clubId !== "string" || typeof name !== "string") {
    redirect("/admin?error=name");
  }

  const api = await apiWithSession();
  const { response } = await api.PUT("/clubs/{clubId}", { params: { path: { clubId } }, body: { name } });
  if (response.status === 400) {
    redirect("/admin?error=name");
  }
  if (!response.ok) {
    throw new Error(`PUT /clubs/${clubId} failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
  redirect("/admin");
}

function userAndRole(formData: FormData): { userId: string; role: "Manager" | "Admin" } | null {
  const userId = formData.get("userId");
  const role = formData.get("role");
  return typeof userId === "string" && (role === "Manager" || role === "Admin") ? { userId, role } : null;
}

/** An Admin makes a User a Manager or an Admin. It takes effect on that User's next request. */
export async function grantRole(formData: FormData) {
  const path = userAndRole(formData);
  if (!path) return;

  const api = await apiWithSession();
  const { response } = await api.PUT("/users/{userId}/roles/{role}", { params: { path } });
  if (!response.ok) {
    throw new Error(`Granting ${path.role} failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

/**
 * An Admin takes a role from a User. The API refuses (409) to remove Admin
 * from the last one or from one set in configuration; the page disables those
 * buttons, so a refusal means the list changed underneath it.
 */
export async function revokeRole(formData: FormData) {
  const path = userAndRole(formData);
  if (!path) return;

  const api = await apiWithSession();
  const { response } = await api.DELETE("/users/{userId}/roles/{role}", { params: { path } });
  if (response.status === 409) {
    redirect("/admin?error=admin-kept");
  }
  if (!response.ok) {
    throw new Error(`Removing ${path.role} failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}
