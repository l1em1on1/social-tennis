/**
 * Runs once per server start, before the first request (Next.js
 * instrumentation `register`).
 *
 * With MOCK_API=1 the API is mocked in-process with MSW (ADR-0009), so the
 * app runs end to end with no `api` service. All API calls happen server-side
 * in this BFF (ADR-0001), so Node-mode interception is the whole story — no
 * browser service worker.
 *
 * MOCK_API is inlined at build time (next.config.ts `env`). In a normal build
 * the condition is constant-false, the dynamic import is dead code, and no MSW
 * code reaches the bundle; CI checks that on the production image.
 */
export async function register() {
  if (process.env.MOCK_API === "1" && process.env.NEXT_RUNTIME === "nodejs") {
    const { server } = await import("./mocks/node");
    // A request with no handler is a gap in the mocks: fail loudly rather than
    // reach the network. (msw 3 renamed v2's onUnhandledRequest to onUnhandledFrame.)
    server.listen({ onUnhandledFrame: "error" });
    console.info("[mocks] MOCK_API=1: API calls are answered by MSW");
  }
}
