/**
 * Name of the HttpOnly cookie holding the opaque API session token. Lives in
 * its own dependency-free module because proxy.ts must not import server-only
 * modules (Next proxy runs outside the RSC runtime).
 */
export const SESSION_COOKIE = "st_session";

/**
 * Absolute URL for a redirect Location. Inside a container the server's own
 * origin is its bind address (0.0.0.0) — which Next uses to absolutize even
 * relative Locations — so redirects must be built from the configured public
 * origin instead.
 */
export const publicUrl = (path: string) =>
  `${process.env.PUBLIC_BASE_URL ?? "http://localhost:3000"}${path}`;
