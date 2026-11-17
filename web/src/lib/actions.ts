"use server";

import { revalidatePath } from "next/cache";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { apiWithSession } from "@/lib/api/server";
import { isId } from "@/lib/ids";
import { ACTING_PLAYER_COOKIE, cookieOptions, sessionCookie } from "@/lib/session";
import { getViewer } from "@/lib/viewer";
import { LEAGUE_TYPES, type LeagueStructure, type LeagueType, type StageLengthUnit } from "@/lib/league";

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

  (await cookies()).set(ACTING_PLAYER_COOKIE, playerId, cookieOptions());
}

export async function logout() {
  const api = await apiWithSession();
  // Revoke server-side first — the credential dies even if a cookie copy
  // survives somewhere — then drop the cookies.
  await api.DELETE("/auth/sessions/current");
  const jar = await cookies();
  // With the attributes they were set with: see cookieOptions.
  jar.delete({ name: sessionCookie(), ...cookieOptions() });
  jar.delete({ name: ACTING_PLAYER_COOKIE, ...cookieOptions() });
  redirect("/login");
}

/** Where a form sits, so its action can send a refusal back to the same page. */
type NameFormPage = "/" | "/profiles";

function nameFormPage(formData: FormData): NameFormPage {
  return formData.get("from") === "/profiles" ? "/profiles" : "/";
}

/** Gender and Level as a form sends them: absent or "" is unset. Null if the value isn't one a form offers. */
function playerDetails(formData: FormData): { gender: "Male" | "Female" | null; level: number | null } | null {
  const gender = formData.get("gender") ?? "";
  const level = formData.get("level") ?? "";
  if ((gender !== "" && gender !== "Male" && gender !== "Female") || typeof level !== "string") {
    return null;
  }
  if (level !== "" && !/^[1-5]$/.test(level)) {
    return null;
  }
  return { gender: gender === "" ? null : gender, level: level === "" ? null : Number(level) };
}

/**
 * Add a Player to this login. The first one becomes the Acting Player by being
 * the only one; later ones don't take over.
 */
export async function createPlayer(formData: FormData) {
  const page = nameFormPage(formData);
  const name = formData.get("name");
  const details = playerDetails(formData);
  if (typeof name !== "string" || !details) {
    redirect(`${page}?error=name`);
  }

  const api = await apiWithSession();
  const { response } = await api.POST("/players", { body: { name, ...details } });
  if (response.status === 400) {
    redirect(`${page}?error=name`);
  }
  // The login already holds as many Players as one may add (#109).
  if (response.status === 409) {
    redirect(`${page}?error=limit`);
  }
  if (!response.ok) {
    throw new Error(`POST /players failed with ${response.status}`);
  }
  // The header's "Playing as" lives in the layout, so every page is stale.
  revalidatePath("/", "layout");
  redirect(page);
}

/**
 * Change the Name, Gender and Level of a Player this login holds. The API
 * replaces all three, so the form sends all three.
 */
export async function updatePlayer(formData: FormData) {
  const playerId = formData.get("playerId");
  const name = formData.get("name");
  const details = playerDetails(formData);
  if (!isId(playerId) || typeof name !== "string" || !details) {
    redirect("/profiles?error=name");
  }

  const api = await apiWithSession();
  const { response } = await api.PUT("/players/{playerId}", {
    params: { path: { playerId } },
    body: { name, ...details },
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
  return isId(clubId) && isId(playerId) ? { clubId, playerId } : null;
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
  if (!isId(clubId) || typeof email !== "string") {
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
  if (!isId(clubId) || !isId(inviteId)) return;

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
  if (!isId(clubId) || typeof name !== "string") {
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
  return isId(userId) && (role === "Manager" || role === "Admin") ? { userId, role } : null;
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

/** A Manager adds a Player to a Club by name and email, for someone who may not have signed in yet. */
export async function addClubPlayer(formData: FormData) {
  const clubId = formData.get("clubId");
  const name = formData.get("name");
  const email = formData.get("email");
  if (!isId(clubId) || typeof name !== "string" || typeof email !== "string") {
    redirect("/manage?error=player");
  }

  const api = await apiWithSession();
  const { response } = await api.POST("/clubs/{clubId}/players", {
    params: { path: { clubId } },
    body: { name, email },
  });
  if (response.status === 400) {
    redirect("/manage?error=player");
  }
  if (!response.ok) {
    throw new Error(`Adding a player to club ${clubId} failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
  redirect(`/manage?added=${encodeURIComponent(name.trim())}`);
}

/** A Manager sets a Player's Rating. Nothing else ever changes it. */
export async function setPlayerRating(formData: FormData) {
  const playerId = formData.get("playerId");
  const rating = formData.get("rating");
  if (!isId(playerId) || typeof rating !== "string" || !/^\d{1,5}$/.test(rating)) {
    redirect("/manage?error=rating");
  }

  const api = await apiWithSession();
  const { response } = await api.PUT("/players/{playerId}/rating", {
    params: { path: { playerId } },
    body: { rating: Number(rating) },
  });
  if (response.status === 400) {
    redirect("/manage?error=rating");
  }
  // 404: the Player was removed meanwhile. The refreshed list shows that.
  if (!response.ok && response.status !== 404) {
    throw new Error(`Setting a rating failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
  redirect("/manage");
}

/** The setup form's fields, as the API takes them. Null if a value isn't one the form offers. */
function leagueFromForm(formData: FormData) {
  const text = (key: string) => {
    const value = formData.get(key);
    return typeof value === "string" ? value : null;
  };
  const type = text("type");
  const structure = text("structure");
  const unit = text("stageUnit");
  const roll = text("stageRoll");
  const name = text("name");
  const startDate = text("startDate");
  const endDate = text("endDate");
  const count = Number(text("stageCount"));
  const gamesPerSet = Number(text("gamesPerSet"));
  const setsPerGame = Number(text("setsPerGame"));
  if (
    name === null ||
    startDate === null ||
    endDate === null ||
    !LEAGUE_TYPES.some((option) => option.value === type) ||
    (structure !== "Divisional" && structure !== "Knockout") ||
    (unit !== "Weeks" && unit !== "Months") ||
    !Number.isInteger(count)
  ) {
    return null;
  }
  return {
    name,
    type: type as LeagueType,
    structure: structure as LeagueStructure,
    startDate,
    endDate,
    stageLength: { count, unit: unit as StageLengthUnit },
    // Only a Divisional League rolls; the API refuses a roll mode on a Knockout one.
    stageRoll: structure === "Divisional" ? (roll === "Automatic" ? "Automatic" : "Manual") : null,
    scoreFormat: {
      gamesPerSet,
      setsPerGame,
      // The switch only exists for best of three; a stale value from a hidden one is dropped.
      matchTiebreakDecider: setsPerGame === 3 && formData.get("matchTiebreakDecider") === "on",
    },
  } as const;
}

/**
 * A Manager creates a League. A refusal comes back as the API's own messages,
 * which the setup page shows next to the fields they name.
 */
export async function createLeague(
  _previous: CreateLeagueState,
  formData: FormData,
): Promise<CreateLeagueState> {
  const clubId = formData.get("clubId");
  const league = leagueFromForm(formData);
  if (!isId(clubId) || !league) {
    return { errors: { form: ["Some of the choices weren't ones this form offers. Reload and try again."] } };
  }

  const api = await apiWithSession();
  const { data, error, response } = await api.POST("/clubs/{clubId}/leagues", {
    params: { path: { clubId } },
    body: league,
  });
  if (response.status === 400 && error && "errors" in error) {
    return { errors: (error.errors ?? {}) as Record<string, string[]> };
  }
  if (!data) {
    throw new Error(`Creating a league failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
  redirect(`/manage/leagues/${data.id}?created=1`);
}

/** The setup form's last refusal, by the API's field names ("structure", "scoreFormat.gamesPerSet"…). */
export interface CreateLeagueState {
  errors: Record<string, string[]>;
}

function leagueAndPlayer(formData: FormData): { leagueId: string; playerId: string } | null {
  const leagueId = formData.get("leagueId");
  const playerId = formData.get("playerId");
  return isId(leagueId) && isId(playerId) ? { leagueId, playerId } : null;
}

/**
 * A Player asks to join a League. The Player comes from the form and is
 * checked against this login's, as for `joinClub`. A 409 means they asked
 * already (another tab): the refreshed page shows that.
 */
export async function askToJoinLeague(formData: FormData) {
  const path = leagueAndPlayer(formData);
  const viewer = await getViewer();
  if (!path || !viewer.players.some((player) => player.id === path.playerId)) {
    return;
  }

  const api = await apiWithSession();
  const { response } = await api.POST("/leagues/{leagueId}/join-requests", {
    params: { path: { leagueId: path.leagueId } },
    body: { playerId: path.playerId },
  });
  if (!response.ok && response.status !== 409) {
    throw new Error(`Asking to join league ${path.leagueId} failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

/**
 * A Manager decides a Join Request, or adds a Club Player straight in. A 409
 * is a race (another Manager decided first, or the Player left the Club), and
 * goes back to the page as a message; a 404 means it's gone, which the
 * refreshed page shows.
 */
async function manageLeagueEntry(formData: FormData, kind: "approval" | "rejection" | "add") {
  const path = leagueAndPlayer(formData);
  if (!path) return;

  const api = await apiWithSession();
  const { response } =
    kind === "add"
      ? await api.PUT("/leagues/{leagueId}/participants/{playerId}", { params: { path } })
      : kind === "approval"
        ? await api.PUT("/leagues/{leagueId}/join-requests/{playerId}/approval", { params: { path } })
        : await api.PUT("/leagues/{leagueId}/join-requests/{playerId}/rejection", { params: { path } });
  if (response.status === 409) {
    redirect(`/manage/leagues/${path.leagueId}?error=conflict`);
  }
  if (!response.ok && response.status !== 404) {
    throw new Error(`League ${kind} failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

export async function approveLeagueJoinRequest(formData: FormData) {
  await manageLeagueEntry(formData, "approval");
}

export async function rejectLeagueJoinRequest(formData: FormData) {
  await manageLeagueEntry(formData, "rejection");
}

export async function addLeagueParticipant(formData: FormData) {
  await manageLeagueEntry(formData, "add");
}

/** A Manager renames a League (#153). Nothing else about it changes. */
export async function renameLeague(formData: FormData) {
  const leagueId = formData.get("leagueId");
  const name = formData.get("name");
  if (!isId(leagueId)) return;
  if (typeof name !== "string") {
    redirect(`/manage/leagues/${leagueId}?error=name`);
  }

  const api = await apiWithSession();
  const { response } = await api.PUT("/leagues/{leagueId}", { params: { path: { leagueId } }, body: { name } });
  if (response.status === 400) {
    redirect(`/manage/leagues/${leagueId}?error=name`);
  }
  if (!response.ok) {
    throw new Error(`PUT /leagues/${leagueId} failed with ${response.status}`);
  }
  // Players' League lists and the Manage page show the name too.
  revalidatePath("/", "layout");
  redirect(`/manage/leagues/${leagueId}`);
}
