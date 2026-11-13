/**
 * Name of the HttpOnly cookie holding the opaque API session token. Lives in
 * its own dependency-free module because proxy.ts must not import server-only
 * modules (Next proxy runs outside the RSC runtime).
 */
export const SESSION_COOKIE = "st_session";

/**
 * Which of the User's Players they are currently acting as (CONTEXT: User —
 * a User may hold several Players). Server-held so every server component
 * reads the same choice; an id the User doesn't hold is ignored, never trusted.
 */
export const ACTING_PLAYER_COOKIE = "st_player";

/**
 * Absolute URL for a redirect Location. Inside a container the server's own
 * origin is its bind address (0.0.0.0) — which Next uses to absolutize even
 * relative Locations — so redirects must be built from the configured public
 * origin instead.
 */
export const publicUrl = (path: string) =>
  `${process.env.PUBLIC_BASE_URL ?? "http://localhost:3000"}${path}`;

/**
 * Cookies are `Secure` exactly when the app is served over https. Keyed to
 * the public origin, not NODE_ENV: every Docker image runs as "production",
 * including over plain http on the compose network, where browsers would store
 * a Secure cookie but never send it back. Behind TLS, PUBLIC_BASE_URL is
 * https (redirects depend on it), so real deployments stay Secure.
 */
export const secureCookies = () => publicUrl("").startsWith("https://");
