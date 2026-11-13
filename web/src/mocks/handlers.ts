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
 * email, so `lib/viewer/stub.ts` personas (manager@…, parent@…) work in mock
 * mode exactly as with the real API.
 */
const http = createOpenApiHttp<paths>({ baseUrl: process.env.API_URL });
const apiOrigin = new URL(process.env.API_URL ?? "http://api:8080").origin;

// Session tokens must survive a cookie round trip unencoded, like the real
// API's base64url tokens do — so the remembered value is base64url too.
const SESSION_PREFIX = "mock-session.";
const MOCK_USER_ID = "00000000-0000-4000-8000-000000000001";

export const handlers = [
  http.get("/clubs", ({ response }) =>
    response(200).json({
      clubs: [{ id: "00000000-0000-4000-8000-0000000000c1", name: "Riverside Tennis Club" }],
      page: { total: 1 },
    }),
  ),

  http.post("/auth/magic-link", ({ response }) => response(202).empty()),

  http.post("/auth/sessions", async ({ request, response }) => {
    const { token } = await request.json();
    return response(200).json({
      token: `${SESSION_PREFIX}${Buffer.from(token).toString("base64url")}`,
      expiresAt: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
    });
  }),

  http.get("/auth/me", ({ request, response }) => {
    const bearer = request.headers.get("Authorization")?.replace(/^Bearer /, "");
    if (!bearer?.startsWith(SESSION_PREFIX)) {
      return response(401).empty();
    }
    const remembered = Buffer.from(bearer.slice(SESSION_PREFIX.length), "base64url").toString();
    return response(200).json({
      userId: MOCK_USER_ID,
      email: remembered.includes("@") ? remembered : "player@example.com",
    });
  }),

  http.delete("/auth/sessions/current", ({ response }) => response(204).empty()),

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
