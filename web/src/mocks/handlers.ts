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
 * email, and the email picks the roles (manager@…, admin@…).
 *
 * Players are kept in memory per session. Every sign-in is a new session and
 * so a new User with no Player, as a first sign-in is with the real API.
 */
const http = createOpenApiHttp<paths>({ baseUrl: process.env.API_URL });
const apiOrigin = new URL(process.env.API_URL ?? "http://api:8080").origin;

// Session tokens must survive a cookie round trip unencoded, like the real
// API's base64url tokens do — so the remembered value is base64url too.
const SESSION_PREFIX = "mock-session.";
const MOCK_USER_ID = "00000000-0000-4000-8000-000000000001";
const MOCK_CLUB = { id: "00000000-0000-4000-8000-0000000000c1", name: "Riverside Tennis Club" };

interface MockPlayer {
  id: string;
  name: string;
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
const invitedEmails = new Map<string, string>();
let sessions = 0;
let playersMade = 0;

function sessionOf(request: Request): string | undefined {
  const bearer = request.headers.get("Authorization")?.replace(/^Bearer /, "");
  return bearer?.startsWith(SESSION_PREFIX) ? bearer : undefined;
}

function emailOf(request: Request): string {
  const encoded = (sessionOf(request) ?? "").slice(SESSION_PREFIX.length).split(".")[0];
  const remembered = Buffer.from(encoded, "base64url").toString();
  return remembered.includes("@") ? remembered.toLowerCase() : "player@example.com";
}

function playersOf(request: Request): MockPlayer[] {
  const session = sessionOf(request) ?? "";
  let players = playersBySession.get(session);
  if (!players) {
    players = [];
    playersBySession.set(session, players);
  }
  return players;
}

const allPlayers = () => [...playersBySession.values()].flat();

const problemJson = { headers: { "Content-Type": "application/problem+json" } };
const invalid = (field: string) => ({ title: "Validation failed", status: 400, errors: { [field]: ["Invalid."] } });

export const handlers = [
  http.get("/clubs", ({ response }) => response(200).json({ clubs: [MOCK_CLUB], page: { total: 1 } })),

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
  http.get("/clubs/{clubId}/members", ({ response }) => {
    const members = allPlayers()
      .filter((player) => player.membership)
      .map((player) => ({
        playerId: player.id,
        name: player.name,
        email: player.email,
        approved: player.membership!.approved,
        requestedAt: player.membership!.requestedAt,
      }));
    const joined = new Set(members.filter((member) => member.approved).map((member) => member.email));
    return response(200).json({
      members,
      invites: [...invitedEmails].map(([email, invitedAt]) => ({ email, invitedAt, joined: joined.has(email) })),
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
    }
    return response(204).empty();
  }),

  http.post("/clubs/{clubId}/invites", async ({ request, response }) => {
    const email = (await request.json()).email.trim().toLowerCase();
    if (!email.includes("@")) {
      return response(400).json(invalid("email"), problemJson);
    }
    if (!invitedEmails.has(email)) {
      invitedEmails.set(email, new Date().toISOString());
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
    // Mock roles come from the email, so a test picks one by who it signs in
    // as: "manager" in the local part is a Manager, "admin" an Admin (who is
    // a Manager too, as with the real API).
    const email = emailOf(request);
    const local = email.split("@")[0].split("+")[0];
    const roles = local.includes("admin") ? ["Admin", "Manager"] : local.includes("manager") ? ["Manager"] : [];
    return response(200).json({ userId: MOCK_USER_ID, email, roles });
  }),

  http.delete("/auth/sessions/current", ({ response }) => response(204).empty()),

  http.get("/players", ({ request, response }) => {
    const players = playersOf(request).map((player) => ({
      id: player.id,
      name: player.name,
      clubs: player.membership ? [{ ...MOCK_CLUB, approved: player.membership.approved }] : [],
    }));
    return response(200).json({ players, page: { total: players.length } });
  }),

  http.post("/players", async ({ request, response }) => {
    const name = (await request.json()).name.trim();
    if (name.length === 0 || name.length > 100) {
      return response(400).json(invalid("name"), problemJson);
    }
    const email = emailOf(request);
    const player: MockPlayer = {
      id: `00000000-0000-4000-8000-${String((playersMade += 1)).padStart(12, "0")}`,
      name,
      email,
      // Invited ahead of time: in the Club from the start.
      membership: invitedEmails.has(email) ? { approved: true, requestedAt: new Date().toISOString() } : undefined,
    };
    playersOf(request).push(player);
    return response(201).json({ id: player.id, name: player.name });
  }),

  http.put("/players/{playerId}", async ({ request, params, response }) => {
    const name = (await request.json()).name.trim();
    if (name.length === 0 || name.length > 100) {
      return response(400).json(invalid("name"), problemJson);
    }
    const player = playersOf(request).find((candidate) => candidate.id === params.playerId);
    if (!player) {
      return response(404).empty();
    }
    player.name = name;
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
