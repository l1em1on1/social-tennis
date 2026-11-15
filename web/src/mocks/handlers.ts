import { http as untypedHttp, passthrough } from "msw";
import { createOpenApiHttp } from "openapi-msw";
import type { paths } from "@/lib/api/schema";

/**
 * Mocked API (ADR-0009), typed against the committed OpenAPI schema: a path,
 * body, or status the real API doesn't declare is a compile error here, so the
 * mocks can't drift from the contract silently.
 *
 * Mock auth has no inbox: any magic-link token redeems, and the session
 * remembers it — when the token is an email address, /auth/me reports that
 * email, and the email picks the starting roles (manager@…, admin@…).
 *
 * Players are kept in memory per session. Every sign-in is a new session and
 * so a new User with no Player, as a first sign-in is with the real API.
 */
const http = createOpenApiHttp<paths>({ baseUrl: process.env.API_URL });
const apiOrigin = new URL(process.env.API_URL ?? "http://api:8080").origin;

// Session tokens must survive a cookie round trip unencoded, like the real
// API's base64url tokens do — so the remembered value is base64url too.
const SESSION_PREFIX = "mock-session.";
const MOCK_CLUB = { id: "00000000-0000-4000-8000-0000000000c1", name: "Riverside Tennis Club" };

interface MockPlayer {
  id: string;
  name: string;
  gender: "Male" | "Female" | null;
  level: number | null;
  rating: number;
  /** Email of the session that made it. */
  email: string;
  /** Present once the Player has asked to join the one Club. */
  membership?: { approved: boolean; requestedAt: string };
}

/**
 * Players by session token, and so per sign-in. The Club's side is shared by
 * every session, as it has to be: a Manager approves in one session what a
 * Player asked for in another.
 */
const playersBySession = new Map<string, MockPlayer[]>();
/** Invites by email. */
const invitedEmails = new Map<string, { id: string; invitedAt: string }>();
let invitesMade = 0;
let sessions = 0;
let playersMade = 0;

function sessionOf(request: Request): string | undefined {
  const bearer = request.headers.get("Authorization")?.replace(/^Bearer /, "");
  return bearer?.startsWith(SESSION_PREFIX) ? bearer : undefined;
}

function emailOfSession(session: string): string {
  const encoded = session.slice(SESSION_PREFIX.length).split(".")[0];
  const remembered = Buffer.from(encoded, "base64url").toString();
  return remembered.includes("@") ? remembered.toLowerCase() : "player@example.com";
}

const emailOf = (request: Request) => emailOfSession(sessionOf(request) ?? "");

const nextPlayerId = () => `00000000-0000-4000-8000-${String((playersMade += 1)).padStart(12, "0")}`;

function playersOf(request: Request): MockPlayer[] {
  const session = sessionOf(request) ?? "";
  let players = playersBySession.get(session);
  if (!players) {
    // Signing in: whatever a Manager added for this email is theirs now.
    const email = emailOf(request);
    players = addedByManager.filter((player) => player.email === email);
    for (const player of players) addedByManager.splice(addedByManager.indexOf(player), 1);
    playersBySession.set(session, players);
  }
  return players;
}

/** Players a Manager added for an email nobody has signed in with yet. */
const addedByManager: MockPlayer[] = [];

const allPlayers = () => [...playersBySession.values()].flat().concat(addedByManager);

const validLevel = (level: unknown): level is number | null | undefined =>
  level === null || level === undefined || (typeof level === "number" && level >= 1 && level <= 5);

/**
 * Users by email, made the first time an email signs in. Roles start from the
 * email, so a test picks one by who it signs in as ("manager" in the local
 * part is a Manager, "admin" an Admin), and an Admin can then change them.
 * `admin@example.com` plays the Admin set in configuration: it can't be demoted.
 */
interface MockUser {
  id: string;
  manager: boolean;
  admin: boolean;
}

const CONFIGURED_ADMIN = "admin@example.com";
const usersByEmail = new Map<string, MockUser>();

function userOf(email: string): MockUser {
  let user = usersByEmail.get(email);
  if (!user) {
    const local = email.split("@")[0].split("+")[0];
    user = {
      id: `00000000-0000-4000-8000-2${String(usersByEmail.size + 1).padStart(11, "0")}`,
      manager: local.includes("manager"),
      admin: local.includes("admin"),
    };
    usersByEmail.set(email, user);
  }
  return user;
}

const userById = (id: string) => [...usersByEmail.values()].find((user) => user.id === id);

/**
 * Clubs an Admin created, by session. Kept to the session that made them, so
 * one test's Club never appears on another test's pages. Nobody can join one.
 */
const clubsBySession = new Map<string, { id: string; name: string }[]>();
let clubsMade = 0;

function clubsOf(request: Request) {
  const session = sessionOf(request) ?? "";
  let clubs = clubsBySession.get(session);
  if (!clubs) {
    clubs = [];
    clubsBySession.set(session, clubs);
  }
  return clubs;
}

const problemJson = { headers: { "Content-Type": "application/problem+json" } };
const invalid = (field: string) => ({ title: "Validation failed", status: 400, errors: { [field]: ["Invalid."] } });

export const handlers = [
  http.get("/clubs", ({ request, response }) => {
    const clubs = [MOCK_CLUB, ...clubsOf(request)];
    return response(200).json({ clubs, page: { total: clubs.length } });
  }),

  http.post("/clubs", async ({ request, response }) => {
    const name = (await request.json()).name.trim();
    if (name.length === 0 || name.length > 200) {
      return response(400).json(invalid("name"), problemJson);
    }
    const club = { id: `00000000-0000-4000-8000-3${String((clubsMade += 1)).padStart(11, "0")}`, name };
    clubsOf(request).push(club);
    return response(201).json(club);
  }),

  http.put("/clubs/{clubId}", async ({ request, params, response }) => {
    const name = (await request.json()).name.trim();
    if (name.length === 0 || name.length > 200) {
      return response(400).json(invalid("name"), problemJson);
    }
    const club = clubsOf(request).find((candidate) => candidate.id === params.clubId);
    if (club) {
      club.name = name;
    } else if (params.clubId !== MOCK_CLUB.id) {
      return response(404).empty();
    }
    // The shared Club keeps its name: every other test's pages show it.
    return response(204).empty();
  }),

  // Asking to join: waits for a Manager unless the email was invited.
  http.put("/clubs/{clubId}/members/{playerId}", ({ request, params, response }) => {
    const player = playersOf(request).find((candidate) => candidate.id === params.playerId);
    if (!player || params.clubId !== MOCK_CLUB.id) {
      return response(404).empty();
    }
    player.membership ??= { approved: invitedEmails.has(player.email), requestedAt: new Date().toISOString() };
    return response(204).empty();
  }),

  // The Manager's side. The mock doesn't check the role: the pages do.
  http.get("/clubs/{clubId}/members", ({ params, response }) => {
    if (params.clubId !== MOCK_CLUB.id) {
      return response(200).json({ members: [], invites: [], page: { total: 0 } });
    }
    const members = allPlayers()
      .filter((player) => player.membership)
      .map((player) => ({
        playerId: player.id,
        name: player.name,
        email: player.email,
        // Players a Manager added sit in no session until that email signs in.
        signedIn: !addedByManager.includes(player),
        approved: player.membership!.approved,
        gender: player.gender,
        level: player.level,
        rating: player.rating,
        requestedAt: player.membership!.requestedAt,
      }));
    const joined = new Set(members.filter((member) => member.approved).map((member) => member.email));
    return response(200).json({
      members,
      invites: [...invitedEmails].map(([email, invite]) => ({ ...invite, email, joined: joined.has(email) })),
      page: { total: members.length },
    });
  }),

  http.put("/clubs/{clubId}/members/{playerId}/approval", ({ params, response }) => {
    const player = allPlayers().find((candidate) => candidate.id === params.playerId);
    if (!player?.membership) {
      return response(404).empty();
    }
    player.membership.approved = true;
    return response(204).empty();
  }),

  http.delete("/clubs/{clubId}/members/{playerId}", ({ params, response }) => {
    const player = allPlayers().find((candidate) => candidate.id === params.playerId);
    if (player) {
      delete player.membership;
      // Removal withdraws the invite too, or it would let them straight back in.
      invitedEmails.delete(player.email);
    }
    return response(204).empty();
  }),

  http.delete("/clubs/{clubId}/invites/{inviteId}", ({ params, response }) => {
    for (const [email, invite] of invitedEmails) {
      if (invite.id === params.inviteId) invitedEmails.delete(email);
    }
    return response(204).empty();
  }),

  http.post("/clubs/{clubId}/players", async ({ request, response }) => {
    const body = await request.json();
    const name = body.name.trim();
    const email = body.email.trim().toLowerCase();
    if (name.length === 0 || name.length > 100 || !email.includes("@")) {
      return response(400).json(invalid("name"), problemJson);
    }
    const player: MockPlayer = {
      id: nextPlayerId(),
      name,
      gender: null,
      level: null,
      rating: 1,
      email,
      membership: { approved: true, requestedAt: new Date().toISOString() },
    };
    // Theirs at once if that email has a session already; otherwise it waits for them.
    const holder = [...playersBySession].find(([session]) => emailOfSession(session) === email);
    (holder ? holder[1] : addedByManager).push(player);
    return response(201).json({ playerId: player.id, name: player.name });
  }),

  http.post("/clubs/{clubId}/invites", async ({ request, response }) => {
    const email = (await request.json()).email.trim().toLowerCase();
    if (!email.includes("@")) {
      return response(400).json(invalid("email"), problemJson);
    }
    if (!invitedEmails.has(email)) {
      invitedEmails.set(email, {
        id: `00000000-0000-4000-8000-1${String((invitesMade += 1)).padStart(11, "0")}`,
        invitedAt: new Date().toISOString(),
      });
    }
    for (const player of allPlayers().filter((candidate) => candidate.email === email)) {
      player.membership = { approved: true, requestedAt: player.membership?.requestedAt ?? new Date().toISOString() };
    }
    return response(204).empty();
  }),

  http.post("/auth/magic-link", ({ response }) => response(202).empty()),

  http.post("/auth/sessions", async ({ request, response }) => {
    const { token } = await request.json();
    return response(200).json({
      // The counter makes every sign-in its own session: base64url has no ".".
      token: `${SESSION_PREFIX}${Buffer.from(token).toString("base64url")}.${(sessions += 1)}`,
      expiresAt: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
    });
  }),

  http.get("/auth/me", ({ request, response }) => {
    if (!sessionOf(request)) {
      return response(401).json({ title: "Unauthorized", status: 401 }, problemJson);
    }
    const email = emailOf(request);
    const user = userOf(email);
    // An Admin is a Manager too, as with the real API.
    const roles = user.admin ? ["Admin", "Manager"] : user.manager ? ["Manager"] : [];
    return response(200).json({ userId: user.id, email, roles });
  }),

  // An Admin's side. The mock doesn't check the role: the pages do.
  http.get("/users", ({ response }) => {
    const users = [...usersByEmail]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([email, user]) => ({
        id: user.id,
        email,
        manager: user.manager,
        admin: user.admin,
        adminBySetting: email === CONFIGURED_ADMIN,
        // Never: the configured Admin can always sign in.
        lastAdmin: false,
        players: allPlayers()
          .filter((player) => player.email === email)
          .map((player) => ({ id: player.id, name: player.name })),
      }));
    return response(200).json({ users, page: { total: users.length } });
  }),

  http.put("/users/{userId}/roles/{role}", ({ params, response }) => {
    const user = userById(params.userId);
    if (!user) {
      return response(404).empty();
    }
    if (params.role === "Admin") user.admin = true;
    else user.manager = true;
    return response(204).empty();
  }),

  http.delete("/users/{userId}/roles/{role}", ({ params, response }) => {
    const user = userById(params.userId);
    if (!user) {
      return response(404).empty();
    }
    if (params.role === "Admin") {
      if (usersByEmail.get(CONFIGURED_ADMIN) === user) {
        return response(409).json({ title: "The Admin role can't be removed from this User.", status: 409 }, problemJson);
      }
      user.admin = false;
    } else {
      user.manager = false;
    }
    return response(204).empty();
  }),

  http.delete("/auth/sessions/current", ({ response }) => response(204).empty()),

  http.get("/players", ({ request, response }) => {
    const players = playersOf(request).map((player) => ({
      id: player.id,
      name: player.name,
      gender: player.gender,
      level: player.level,
      clubs: player.membership ? [{ ...MOCK_CLUB, approved: player.membership.approved }] : [],
    }));
    return response(200).json({ players, page: { total: players.length } });
  }),

  http.post("/players", async ({ request, response }) => {
    const body = await request.json();
    const name = body.name.trim();
    if (name.length === 0 || name.length > 100 || !validLevel(body.level)) {
      return response(400).json(invalid("name"), problemJson);
    }
    const email = emailOf(request);
    const player: MockPlayer = {
      id: nextPlayerId(),
      name,
      gender: body.gender ?? null,
      level: body.level ?? null,
      rating: 1,
      email,
      // Invited ahead of time: in the Club from the start.
      membership: invitedEmails.has(email) ? { approved: true, requestedAt: new Date().toISOString() } : undefined,
    };
    playersOf(request).push(player);
    return response(201).json({ id: player.id, name: player.name });
  }),

  http.put("/players/{playerId}", async ({ request, params, response }) => {
    const body = await request.json();
    const name = body.name.trim();
    if (name.length === 0 || name.length > 100 || !validLevel(body.level)) {
      return response(400).json(invalid("name"), problemJson);
    }
    const player = playersOf(request).find((candidate) => candidate.id === params.playerId);
    if (!player) {
      return response(404).empty();
    }
    player.name = name;
    player.gender = body.gender ?? null;
    player.level = body.level ?? null;
    return response(204).empty();
  }),

  http.put("/players/{playerId}/rating", async ({ request, params, response }) => {
    const { rating } = await request.json();
    if (typeof rating !== "number" || rating < 1 || rating > 10_000) {
      return response(400).json(invalid("rating"), problemJson);
    }
    const player = allPlayers().find((candidate) => candidate.id === params.playerId);
    if (!player) {
      return response(404).empty();
    }
    player.rating = rating;
    return response(204).empty();
  }),

  http.delete("/players/{playerId}/clubs/{clubId}", ({ request, params, response }) => {
    const player = playersOf(request).find((candidate) => candidate.id === params.playerId);
    if (!player) {
      return response(404).empty();
    }
    delete player.membership;
    return response(204).empty();
  }),

  // Last: only the API is mocked. Next.js also fetches its own origin
  // server-side (a server action's redirect loads the target's RSC payload),
  // and that must reach the real server. The predicate keeps this handler off
  // the API origin entirely: a handler that *matches* and returns nothing
  // counts as handled and lets the request through, so an API miss would
  // silently reach the network. Unmatched, it hits `onUnhandledFrame: "error"`
  // — a gap in the mocks (MSW docs, custom predicate).
  untypedHttp.all(
    ({ request }) => new URL(request.url).origin !== apiOrigin,
    () => passthrough(),
  ),
];
