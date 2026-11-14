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
 * email, so `lib/viewer/stub.ts` personas (manager@…, admin@…) work in mock
 * mode exactly as with the real API.
 *
 * Players and Club membership are kept in memory per session. Every sign-in
 * is a new session and so a new User with no Player, as a first sign-in is
 * with the real API. Nothing carries over between tests.
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
  clubs: { id: string; name: string }[];
}

/** Players by session token. */
const playersBySession = new Map<string, MockPlayer[]>();
let sessions = 0;

function sessionOf(request: Request): string | undefined {
  const bearer = request.headers.get("Authorization")?.replace(/^Bearer /, "");
  return bearer?.startsWith(SESSION_PREFIX) ? bearer : undefined;
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

const problemJson = { headers: { "Content-Type": "application/problem+json" } };

export const handlers = [
  http.get("/clubs", ({ response }) =>
    response(200).json({ clubs: [MOCK_CLUB], page: { total: 1 } }),
  ),

  http.put("/clubs/{clubId}/members/{playerId}", ({ request, params, response }) => {
    const player = playersOf(request).find((candidate) => candidate.id === params.playerId);
    if (!player || params.clubId !== MOCK_CLUB.id) {
      return response(404).empty();
    }
    if (!player.clubs.some((club) => club.id === MOCK_CLUB.id)) {
      player.clubs.push(MOCK_CLUB);
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
    const session = sessionOf(request);
    if (!session) {
      return response(401).json({ title: "Unauthorized", status: 401 }, problemJson);
    }
    const encoded = session.slice(SESSION_PREFIX.length).split(".")[0];
    const remembered = Buffer.from(encoded, "base64url").toString();
    return response(200).json({
      userId: MOCK_USER_ID,
      email: remembered.includes("@") ? remembered : "player@example.com",
    });
  }),

  http.delete("/auth/sessions/current", ({ response }) => response(204).empty()),

  http.get("/players", ({ request, response }) => {
    const players = playersOf(request);
    return response(200).json({ players, page: { total: players.length } });
  }),

  http.post("/players", async ({ request, response }) => {
    const name = (await request.json()).name.trim();
    if (name.length === 0 || name.length > 100) {
      return response(400).json({ title: "Validation failed", status: 400, errors: { name: ["Invalid."] } }, problemJson);
    }
    const players = playersOf(request);
    // Stable ids, not random ones: Chromatic snapshots the DOM, hidden inputs included.
    const player: MockPlayer = {
      id: `00000000-0000-4000-8000-${String(players.length + 1).padStart(12, "0")}`,
      name,
      clubs: [],
    };
    players.push(player);
    return response(201).json({ id: player.id, name: player.name });
  }),

  http.put("/players/{playerId}", async ({ request, params, response }) => {
    const name = (await request.json()).name.trim();
    if (name.length === 0 || name.length > 100) {
      return response(400).json({ title: "Validation failed", status: 400, errors: { name: ["Invalid."] } }, problemJson);
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
