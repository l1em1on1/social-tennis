"use server";

import { revalidatePath } from "next/cache";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { adminHref } from "@/lib/admin-href";
import { apiWithSession } from "@/lib/api/server";
import { isId } from "@/lib/ids";
import { isJoinCode, joinPath, type NameFormPage } from "@/lib/join";
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
function nameFormPage(formData: FormData): NameFormPage {
  const from = formData.get("from");
  if (from === "/profiles") return "/profiles";
  const code = typeof from === "string" ? /^\/join\/(.+)$/.exec(from)?.[1] : undefined;
  return isJoinCode(code) ? joinPath(code) : "/";
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
 * One of this login's Players asks to join a Club through its join link
 * (#222, ADR-0032). A Manager approves it, unless the User's email was
 * pre-approved or the User is in the Club already: then they're in at once.
 * Pressing twice asks once: the API's PUT is idempotent.
 *
 * The Player comes from the form, not the
 * Acting Player cookie: the page may have been rendered for a Player since
 * switched away from in another tab, and the request must be for the one the
 * person saw. It is checked against the Players this login holds, as
 * `switchActingPlayer` does.
 */
export async function askToJoinClub(formData: FormData) {
  const code = formData.get("code");
  const playerId = formData.get("playerId");
  const viewer = await getViewer();
  if (!isJoinCode(code) || !isId(playerId) || !viewer.players.some((player) => player.id === playerId)) {
    return;
  }

  const api = await apiWithSession();
  const { response } = await api.PUT("/join-links/{code}/members/{playerId}", { params: { path: { code, playerId } } });
  // 404: the link was turned off or reset since the page loaded; the page now says so.
  if (!response.ok && response.status !== 404) {
    throw new Error(`Asking to join through a link failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

/** A Manager turns a Club's join link on (#222). One that's on already stays as it is. */
export async function turnOnJoinLink(formData: FormData) {
  const clubId = formData.get("clubId");
  if (!isId(clubId)) return;

  const api = await apiWithSession();
  const { response } = await api.PUT("/clubs/{clubId}/join-link", { params: { path: { clubId } } });
  if (!response.ok) {
    throw new Error(`Turning on a join link failed with ${response.status}`);
  }
  revalidatePath("/manage");
}

/** A Manager resets a Club's join link: a new link, and the old one stops working (#222). */
export async function resetJoinLink(formData: FormData) {
  const clubId = formData.get("clubId");
  if (!isId(clubId)) return;

  const api = await apiWithSession();
  const { response } = await api.POST("/clubs/{clubId}/join-link/reset", { params: { path: { clubId } } });
  if (!response.ok) {
    throw new Error(`Resetting a join link failed with ${response.status}`);
  }
  revalidatePath("/manage");
}

/** A Manager turns a Club's join link off: nobody can ask to join until it's on again (#222). */
export async function turnOffJoinLink(formData: FormData) {
  const clubId = formData.get("clubId");
  if (!isId(clubId)) return;

  const api = await apiWithSession();
  const { response } = await api.DELETE("/clubs/{clubId}/join-link", { params: { path: { clubId } } });
  if (!response.ok) {
    throw new Error(`Turning off a join link failed with ${response.status}`);
  }
  revalidatePath("/manage");
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

/**
 * A Manager lets someone into a Club by email, on Manage's "Add someone" form
 * (#224). With a name, that Player is added now (`POST …/players`), and the
 * address's other Players come in with it; a name the address already holds
 * adds nobody new. Without one, the email is pre-approved (`POST …/invites`):
 * its Players are in now, or once they sign in and save a name.
 */
export async function addToClub(formData: FormData) {
  const clubId = formData.get("clubId");
  const email = formData.get("email");
  const name = formData.get("name") ?? "";
  if (!isId(clubId) || typeof email !== "string" || typeof name !== "string") {
    redirect("/manage?error=email");
  }

  const api = await apiWithSession();
  const params = { params: { path: { clubId } } };
  if (name.trim() === "") {
    const { response } = await api.POST("/clubs/{clubId}/invites", { ...params, body: { email } });
    if (response.status === 400) {
      redirect("/manage?error=email");
    }
    if (!response.ok) {
      throw new Error(`Inviting to club ${clubId} failed with ${response.status}`);
    }
    revalidatePath("/", "layout");
    redirect(`/manage?invited=${encodeURIComponent(email.trim())}`);
  }

  const { data, response } = await api.POST("/clubs/{clubId}/players", { ...params, body: { name, email } });
  if (response.status === 400) {
    redirect("/manage?error=player");
  }
  if (!response.ok || !data) {
    throw new Error(`Adding a player to club ${clubId} failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
  // The Player's name as held: one the address had already keeps its own spelling.
  redirect(`/manage?added=${encodeURIComponent(data.name)}`);
}

/** The Club and User a role form names, for a page that gives and takes League manager roles (#220). */
function clubAndUser(formData: FormData) {
  const clubId = formData.get("clubId");
  const userId = formData.get("userId");
  return isId(clubId) && isId(userId) ? { clubId, userId } : null;
}

/** A Club's manager lets a member of it create Leagues there (#220), and run those they create. */
export async function grantLeagueManagerRole(formData: FormData) {
  const path = clubAndUser(formData);
  if (!path) return;

  const api = await apiWithSession();
  const { response } = await api.PUT("/clubs/{clubId}/league-managers/{userId}", { params: { path } });
  // 409: their last player left the Club meanwhile. The refreshed list shows that.
  if (!response.ok && response.status !== 409 && response.status !== 404) {
    throw new Error(`Making a League manager failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

/** A Club's manager takes the League manager role (#220). Leagues they run stay theirs. */
export async function removeLeagueManagerRole(formData: FormData) {
  const path = clubAndUser(formData);
  if (!path) return;

  const api = await apiWithSession();
  const { response } = await api.DELETE("/clubs/{clubId}/league-managers/{userId}", { params: { path } });
  if (!response.ok && response.status !== 404) {
    throw new Error(`Removing a League manager failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

/** The League and User a form names, for who runs a League (#220). */
function leagueAndUser(formData: FormData) {
  const leagueId = formData.get("leagueId");
  const userId = formData.get("userId");
  return isId(leagueId) && isId(userId) ? { leagueId, userId } : null;
}

/** A manager of the League's Club gives someone in the Club this League to run (#220). */
export async function assignLeagueManager(formData: FormData) {
  const path = leagueAndUser(formData);
  if (!path) return;

  const api = await apiWithSession();
  const { response } = await api.PUT("/leagues/{leagueId}/managers/{userId}", { params: { path } });
  if (response.status === 409) {
    redirect(`/manage/leagues/${path.leagueId}?error=not-in-club`);
  }
  if (!response.ok && response.status !== 404) {
    throw new Error(`Assigning a League failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

/** A manager of the League's Club takes a League back from someone who runs it (#220). */
export async function unassignLeagueManager(formData: FormData) {
  const path = leagueAndUser(formData);
  if (!path) return;

  const api = await apiWithSession();
  const { response } = await api.DELETE("/leagues/{leagueId}/managers/{userId}", { params: { path } });
  if (!response.ok && response.status !== 404) {
    throw new Error(`Taking a League back failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
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

/** An Admin makes a User a manager of a Club (#110). It takes effect on that User's next request. */
export async function makeClubManager(formData: FormData) {
  const userId = formData.get("userId");
  const clubId = formData.get("clubId");
  if (!isId(userId) || !isId(clubId)) return;

  const api = await apiWithSession();
  const { response } = await api.PUT("/clubs/{clubId}/managers/{userId}", { params: { path: { clubId, userId } } });
  if (!response.ok) {
    throw new Error(`Making a Club's manager failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

/** An Admin takes a User's role as manager of a Club (#110). */
export async function removeClubManager(formData: FormData) {
  const userId = formData.get("userId");
  const clubId = formData.get("clubId");
  if (!isId(userId) || !isId(clubId)) return;

  const api = await apiWithSession();
  const { response } = await api.DELETE("/clubs/{clubId}/managers/{userId}", { params: { path: { clubId, userId } } });
  if (!response.ok) {
    throw new Error(`Removing a Club's manager failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

/** An Admin makes a User an Admin. It takes effect on that User's next request. */
export async function grantAdmin(formData: FormData) {
  const userId = formData.get("userId");
  if (!isId(userId)) return;

  const api = await apiWithSession();
  const { response } = await api.PUT("/users/{userId}/roles/{role}", { params: { path: { userId, role: "Admin" } } });
  if (!response.ok) {
    throw new Error(`Granting Admin failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

/**
 * An Admin takes the Admin role from a User. The API refuses (409) to remove it
 * from the last one or from one set in configuration; the page disables those
 * buttons, so a refusal means the list changed underneath it.
 */
export async function revokeAdmin(formData: FormData) {
  const userId = formData.get("userId");
  if (!isId(userId)) return;

  const api = await apiWithSession();
  const { response } = await api.DELETE("/users/{userId}/roles/{role}", { params: { path: { userId, role: "Admin" } } });
  if (response.status === 409) {
    // Back to the search and page the Admin was on (#134), with the reason.
    const search = formData.get("q");
    const offset = formData.get("offset");
    redirect(
      adminHref({
        error: "admin-kept",
        search: typeof search === "string" ? search.trim() : "",
        offset: typeof offset === "string" ? Math.max(0, Number.parseInt(offset, 10) || 0) : 0,
      }),
    );
  }
  if (!response.ok) {
    throw new Error(`Removing Admin failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

/**
 * An Admin deletes a User (#116, ADR-0024): their Players show as "Former
 * player" in results, and their sign-in ends. The API refuses (409) the
 * Admin's own account, an Admin by configuration and the last Admin; the page
 * offers none of those, so a refusal means the list changed underneath it.
 */
export async function deleteUser(formData: FormData) {
  const userId = formData.get("userId");
  if (!isId(userId)) return;

  const api = await apiWithSession();
  const { response } = await api.DELETE("/users/{userId}", { params: { path: { userId } } });
  if (response.status === 409) {
    const search = formData.get("q");
    const offset = formData.get("offset");
    redirect(
      adminHref({
        error: "user-kept",
        search: typeof search === "string" ? search.trim() : "",
        offset: typeof offset === "string" ? Math.max(0, Number.parseInt(offset, 10) || 0) : 0,
      }),
    );
  }
  // 404: another Admin deleted them first. The refreshed list shows that.
  if (!response.ok && response.status !== 404) {
    throw new Error(`Deleting a user failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
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
  // Only a Divisional League of Teams has a Division size (ADR-0031); the API refuses one otherwise.
  const divisionSize =
    structure === "Divisional" && type !== "Foursomes" && text("divisionSize") ? Number(text("divisionSize")) : null;
  if (
    name === null ||
    startDate === null ||
    endDate === null ||
    !LEAGUE_TYPES.some((option) => option.value === type) ||
    (structure !== "Divisional" && structure !== "Knockout") ||
    (unit !== "Weeks" && unit !== "Months") ||
    !Number.isInteger(count) ||
    (divisionSize !== null && !Number.isInteger(divisionSize))
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
    divisionSize,
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
 * The refusals of asking to join that the Leagues page explains (#6): each
 * concerns the partner named, or the Player being named by someone else.
 * Anything else (asked already, already in) the refreshed page shows.
 */
const JOIN_REFUSALS = ["partner-in-a-team", "partner-in-a-request", "named-as-partner"] as const;
export type JoinRefusal = (typeof JOIN_REFUSALS)[number] | "partner-gone";

/**
 * A Player asks to join a League, alone or, in a doubles League, with the
 * partner picked by name (#6). The Player comes from the form and is checked
 * against this login's, as for `askToJoinClub`; the partner is checked by the API.
 * A refusal about the partner goes back to the page as `?error=`.
 */
export async function askToJoinLeague(formData: FormData) {
  const path = leagueAndPlayer(formData);
  const viewer = await getViewer();
  if (!path || !viewer.players.some((player) => player.id === path.playerId)) {
    return;
  }
  const partner = formData.get("partnerId");
  const partnerId = isId(partner) ? partner : undefined;
  if (partner !== null && partner !== "" && !partnerId) {
    return;
  }

  const api = await apiWithSession();
  const { error, response } = await api.POST("/leagues/{leagueId}/join-requests", {
    params: { path: { leagueId: path.leagueId } },
    body: { playerId: path.playerId, partnerId },
  });
  const refusal = (JOIN_REFUSALS as readonly string[]).find((type) => type === error?.type);
  if (refusal) {
    redirect(`/leagues?error=${refusal}`);
  }
  // A partner who has left the Club since the page loaded.
  if (response.status === 404 && partnerId) {
    redirect("/leagues?error=partner-gone");
  }
  if (!response.ok && response.status !== 409) {
    throw new Error(`Asking to join league ${path.leagueId} failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

/**
 * The Player who asked to join a League, or the partner they named, takes the
 * request back while it waits (#6). The API checks that this login holds one
 * of them. A 404 or 409 means it's gone or decided: the refreshed page shows that.
 */
export async function takeBackLeagueJoinRequest(formData: FormData) {
  const path = leagueAndPlayer(formData);
  if (!path) return;

  const api = await apiWithSession();
  const { response } = await api.DELETE("/leagues/{leagueId}/join-requests/{playerId}", { params: { path } });
  if (!response.ok && response.status !== 404 && response.status !== 409) {
    throw new Error(`Taking back the request to join league ${path.leagueId} failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

/**
 * A Manager decides a Join Request, or adds a Club Player straight in. A 409
 * is a race (another Manager decided first, or the Player left the Club), and
 * goes back to the page as a message; a 404 means it's gone, which the
 * refreshed page shows. A pair that can't be approved because of the partner
 * (#6) gets its own message, so the Manager knows pressing again won't help.
 */
async function manageLeagueEntry(formData: FormData, kind: "approval" | "rejection" | "add" | "remove") {
  const path = leagueAndPlayer(formData);
  if (!path) return;

  const api = await apiWithSession();
  const { error, response } =
    kind === "add"
      ? await api.PUT("/leagues/{leagueId}/participants/{playerId}", { params: { path } })
      : kind === "remove"
        ? await api.DELETE("/leagues/{leagueId}/participants/{playerId}", { params: { path } })
        : kind === "approval"
          ? await api.PUT("/leagues/{leagueId}/join-requests/{playerId}/approval", { params: { path } })
          : await api.PUT("/leagues/{leagueId}/join-requests/{playerId}/rejection", { params: { path } });
  if (response.status === 409) {
    const reason =
      error?.type === "already-in-a-team"
        ? "partner-paired"
        : error?.type === "partner-not-a-club-member"
          ? "partner-gone"
          : error?.type === "on-the-ladder"
            ? "on-the-ladder"
            : "conflict";
    redirect(`/manage/leagues/${path.leagueId}?error=${reason}`);
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

/** A Manager takes a Player out of a League before they play (#159): before the start, or off its ladder after it (#7). They may ask to join again. */
export async function removeLeagueParticipant(formData: FormData) {
  await manageLeagueEntry(formData, "remove");
}

/**
 * A Manager makes a Team of two unpaired Players in a doubles League (#6). A
 * Player already paired, or no longer in the League, comes back as a conflict;
 * the same Player twice, as a bad pairing.
 */
export async function pairTeam(formData: FormData) {
  const leagueId = formData.get("leagueId");
  const firstPlayerId = formData.get("firstPlayerId");
  const secondPlayerId = formData.get("secondPlayerId");
  if (!isId(leagueId)) return;
  if (!isId(firstPlayerId) || !isId(secondPlayerId) || firstPlayerId === secondPlayerId) {
    redirect(`/manage/leagues/${leagueId}?error=pair`);
  }

  const api = await apiWithSession();
  const { response } = await api.POST("/leagues/{leagueId}/teams", {
    params: { path: { leagueId } },
    body: { firstPlayerId, secondPlayerId },
  });
  if (response.status === 409) {
    redirect(`/manage/leagues/${leagueId}?error=conflict`);
  }
  if (response.status === 400) {
    redirect(`/manage/leagues/${leagueId}?error=pair`);
  }
  if (!response.ok && response.status !== 404) {
    throw new Error(`POST /leagues/${leagueId}/teams failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

/** A Manager breaks up a doubles Team (#6): both Players stay in the League, unpaired. */
export async function unpairTeam(formData: FormData) {
  const leagueId = formData.get("leagueId");
  const teamId = formData.get("teamId");
  if (!isId(leagueId) || !isId(teamId)) return;

  const api = await apiWithSession();
  const { error, response } = await api.DELETE("/leagues/{leagueId}/teams/{teamId}", {
    params: { path: { leagueId, teamId } },
  });
  if (response.status === 409) {
    redirect(`/manage/leagues/${leagueId}?error=${error?.type === "on-the-ladder" ? "on-the-ladder" : "conflict"}`);
  }
  if (!response.ok && response.status !== 404) {
    throw new Error(`DELETE /leagues/${leagueId}/teams/${teamId} failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
}

/**
 * A Manager starts a Divisional League with the ladder they confirmed (#7):
 * `ladder` in order, top first, its Division sizes (ADR-0031), then who they
 * left out and who the start removes, as the Start screen showed them. If the League changed since, the
 * API refuses (`league-changed`) and the screen reloads with the new proposal.
 */
export async function startLeague(formData: FormData) {
  const leagueId = formData.get("leagueId");
  if (!isId(leagueId)) return;
  const ids = (field: string) => formData.getAll(field).filter(isId);

  const api = await apiWithSession();
  const { error, response } = await api.POST("/leagues/{leagueId}/start", {
    params: { path: { leagueId } },
    body: {
      ladder: ids("ladder"),
      divisions: formData.getAll("divisions").map(Number).filter(Number.isInteger),
      leftOut: ids("leftOut"),
      removed: ids("removed"),
    },
  });
  if (error?.type === "league-changed") {
    redirect(`/manage/leagues/${leagueId}/start?error=changed`);
  }
  if (response.status === 400) {
    redirect(`/manage/leagues/${leagueId}/start?error=ladder`);
  }
  // Already started (another Manager, another tab): the League's page shows it.
  if (!response.ok && response.status !== 409 && response.status !== 404) {
    throw new Error(`POST /leagues/${leagueId}/start failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
  redirect(`/manage/leagues/${leagueId}${response.ok ? "?started=1" : ""}`);
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

/** The score form's last refusal, by the API's field names ("playedOn", "sets", "sets[1]"…); "form" is the form's own. */
export interface SubmitScoreState {
  errors: Record<string, string[]>;
}

/** A whole number of up to `digits` digits from a form field, or null when it's blank or isn't one. */
function wholeNumber(value: FormDataEntryValue | null | undefined, digits = 3): number | null {
  return typeof value === "string" && new RegExp(`^\\d{1,${digits}}$`).test(value) ? Number(value) : null;
}

/** Where the Match screen explains a refusal it was sent back with (#38). */
const SCORE_REFUSALS: Record<string, string> = {
  "already-scored": "already-scored",
  "stage-closed": "stage-closed",
  "not-scored": "not-scored",
  "not-played": "not-played",
};

/**
 * A Score from the score form (#38): entered by a Player who played the Game,
 * corrected by one while the Stage is open, overridden by a Manager, or
 * recorded by a Manager for a Game nobody scored (#10). A
 * refusal of the Score itself comes back as the API's own messages, which the
 * form shows by the field they name. A Score changed since the form opened
 * reopens the form with it as it is now (ADR-0018); any other refusal sends
 * them back to the Match screen to see why.
 */
export async function submitScore(_previous: SubmitScoreState, formData: FormData): Promise<SubmitScoreState> {
  const gameId = formData.get("gameId");
  const mode = formData.get("mode") ?? "enter";
  const playerId = formData.get("playerId");
  const version = wholeNumber(formData.get("version"), 10);
  const playedOn = formData.get("playedOn");
  const firsts = formData.getAll("first").map((value) => wholeNumber(value));
  const seconds = formData.getAll("second").map((value) => wholeNumber(value));
  const firstTiebreaks = formData.getAll("firstTiebreak");
  const secondTiebreaks = formData.getAll("secondTiebreak");
  const winner = formData.get("retirementWinner");
  const invalid = { errors: { form: ["Some of the values weren't ones this form offers. Reload and try again."] } };
  if (!isId(gameId) || typeof playedOn !== "string" || !/^\d{4}-\d{2}-\d{2}$/.test(playedOn)) {
    return invalid;
  }
  if (
    (mode !== "enter" && mode !== "correct" && mode !== "override" && mode !== "record") ||
    ((mode === "enter" || mode === "correct") && !isId(playerId)) ||
    ((mode === "correct" || mode === "override") && version === null) ||
    firsts.length !== seconds.length ||
    firstTiebreaks.length !== firsts.length ||
    secondTiebreaks.length !== firsts.length ||
    firsts.some((value) => value === null) ||
    seconds.some((value) => value === null) ||
    (winner !== null && winner !== "1" && winner !== "2")
  ) {
    return invalid;
  }

  const entry = {
    playedOn,
    sets: firsts.map((first, index) => ({
      first: first!,
      second: seconds[index]!,
      firstTiebreak: wholeNumber(firstTiebreaks[index]),
      secondTiebreak: wholeNumber(secondTiebreaks[index]),
    })),
    retirementWinner: winner === null ? null : Number(winner),
  };
  const api = await apiWithSession();
  const path = { params: { path: { gameId } } };
  const { error, response } =
    mode === "enter"
      ? await api.POST("/games/{gameId}/score", { ...path, body: { ...entry, playerId: playerId as string } })
      : mode === "correct"
        ? await api.PUT("/games/{gameId}/score", { ...path, body: { ...entry, playerId: playerId as string, version: version! } })
        : mode === "override"
          ? await api.PUT("/games/{gameId}/score/override", { ...path, body: { ...entry, version: version! } })
          : await api.POST("/games/{gameId}/score/record", { ...path, body: entry });
  if (response.status === 400 && error && "errors" in error) {
    return { errors: (error.errors ?? {}) as Record<string, string[]> };
  }
  const refusal = error && "type" in error && typeof error.type === "string" ? error.type : null;
  if (refusal === "score-changed") {
    redirect(`/games/${gameId}/score?changed=1`);
  }
  if (response.status === 403 || response.status === 409) {
    redirect(`/games/${gameId}?refused=${(refusal && SCORE_REFUSALS[refusal]) ?? "not-in-game"}`);
  }
  if (!response.ok && response.status !== 404) {
    throw new Error(`Saving the score of game ${gameId} failed with ${response.status}`);
  }
  // The ladder, the Leagues list and the Match screen all show it.
  revalidatePath("/", "layout");
  redirect(`/games/${gameId}${response.ok ? `?saved=${mode}` : ""}`);
}

/**
 * A Manager marks a Game not played, or undoes that (#10). The Match screen
 * it came from shows the result; a Game scored or a Stage closed meanwhile
 * comes back as the refusal it is.
 */
async function setPlayed(formData: FormData, played: boolean) {
  const gameId = formData.get("gameId");
  if (!isId(gameId)) return;

  const api = await apiWithSession();
  const path = { params: { path: { gameId } } };
  const { error, response } = played ? await api.DELETE("/games/{gameId}/not-played", path) : await api.PUT("/games/{gameId}/not-played", path);
  const refusal = error && "type" in error && typeof error.type === "string" ? SCORE_REFUSALS[error.type] : undefined;
  if (response.status === 409) {
    redirect(`/games/${gameId}?refused=${refusal ?? "stage-closed"}`);
  }
  if (!response.ok && response.status !== 404) {
    throw new Error(`Marking game ${gameId} ${played ? "played" : "not played"} failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
  redirect(`/games/${gameId}${response.ok ? `?saved=${played ? "unvoid" : "void"}` : ""}`);
}

export async function voidGame(formData: FormData) {
  await setPlayed(formData, false);
}

export async function unvoidGame(formData: FormData) {
  await setPlayed(formData, true);
}

/** The close form's last refusal, by the API's field names ("nextStartDate"…); "form" is the form's own. */
export interface CloseStageState {
  errors: Record<string, string[]>;
}

/**
 * A Manager closes the current Stage (#10) as the close screen showed it:
 * each Division's final order, the waiting Teams they ticked, and the next
 * Stage's dates. Dates the API refuses come back to the form; a close that no
 * longer matches the League (a score changed, a Team went) reloads the screen
 * and says so.
 */
export async function closeStage(_previous: CloseStageState, formData: FormData): Promise<CloseStageState> {
  const leagueId = formData.get("leagueId");
  const divisions = formData.getAll("division").map((value) => (typeof value === "string" && value ? value.split(",") : []));
  const joining = formData.getAll("joining").filter(isId);
  const start = formData.get("nextStartDate");
  const end = formData.get("nextEndDate");
  const date = (value: FormDataEntryValue | null) => (typeof value === "string" && /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : null);
  if (!isId(leagueId) || divisions.length === 0 || divisions.some((division) => division.length === 0 || !division.every(isId))) {
    return { errors: { form: ["Some of the values weren't ones this form offers. Reload and try again."] } };
  }

  const api = await apiWithSession();
  const { error, response } = await api.POST("/leagues/{leagueId}/close", {
    params: { path: { leagueId } },
    body: { divisions, joining, nextStartDate: date(start), nextEndDate: date(end) },
  });
  if (response.status === 400 && error && "errors" in error) {
    return { errors: (error.errors ?? {}) as Record<string, string[]> };
  }
  const refusal = error && "type" in error && typeof error.type === "string" ? error.type : null;
  if (refusal === "standings-changed" || refusal === "league-changed") {
    // The dates don't depend on what changed: the reloaded screen keeps the ones typed (#217 review).
    const dates = [date(start) && `start=${date(start)}`, date(end) && `end=${date(end)}`].filter(Boolean).join("&");
    redirect(`/manage/leagues/${leagueId}/close?error=changed${dates ? `&${dates}` : ""}`);
  }
  if (refusal === "stage-queue-not-empty") {
    redirect(`/manage/leagues/${leagueId}/close?error=queue`);
  }
  if (!response.ok && response.status !== 404 && response.status !== 409) {
    throw new Error(`POST /leagues/${leagueId}/close failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
  redirect(`/manage/leagues/${leagueId}${response.ok ? "?closed=1" : ""}`);
}

/** The move refusals the Manager's League page explains (#10), by the API's type. */
const MOVE_REFUSALS = ["team-has-results", "division-too-small", "not-on-the-ladder", "stage-closed"] as const;

/**
 * A Manager moves a Team on the ladder to another Division mid-Stage (#10).
 * The League's page says what happened, or why it couldn't.
 */
export async function moveTeam(formData: FormData) {
  const leagueId = formData.get("leagueId");
  const teamId = formData.get("teamId");
  const toDivision = Number(formData.get("toDivision"));
  if (!isId(leagueId)) return;
  if (!isId(teamId) || !Number.isInteger(toDivision) || toDivision < 1) {
    redirect(`/manage/leagues/${leagueId}?moveRefused=invalid`);
  }

  const api = await apiWithSession();
  const { error, response } = await api.POST("/leagues/{leagueId}/moves", {
    params: { path: { leagueId } },
    body: { teamId, toDivision },
  });
  if (response.status === 400) {
    redirect(`/manage/leagues/${leagueId}?moveRefused=invalid`);
  }
  if (response.status === 409) {
    const type = error && "type" in error && typeof error.type === "string" ? error.type : "";
    const known = (MOVE_REFUSALS as readonly string[]).includes(type) ? type : "changed";
    redirect(`/manage/leagues/${leagueId}?moveRefused=${known}`);
  }
  if (!response.ok && response.status !== 404) {
    throw new Error(`POST /leagues/${leagueId}/moves failed with ${response.status}`);
  }
  revalidatePath("/", "layout");
  redirect(`/manage/leagues/${leagueId}${response.ok ? `?moved=${teamId}` : ""}`);
}
