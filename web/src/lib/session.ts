/**
 * Name of the HttpOnly cookie holding the opaque API session token. Lives in
 * its own dependency-free module because proxy.ts must not import server-only
 * modules (Next proxy runs outside the RSC runtime).
 *
 * Over https it carries the `__Host-` prefix (#53): the browser then accepts
 * it only if it is Secure, has Path=/ and no Domain, so no other subdomain can
 * plant a session cookie for this app. Over plain http (the compose network)
 * a Secure cookie is never sent back, so the prefix can't be used there.
 */
export const sessionCookie = () => (secureCookies() ? "__Host-st_session" : "st_session");

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

/**
 * The attributes the session and acting-Player cookies are set with, and must
 * be cleared with: a browser accepts a `__Host-` cookie, its expiry included,
 * only with Secure and Path=/. `cookies().delete(name)` alone sends neither
 * Secure nor HttpOnly, so logging out over https would leave the cookie in
 * place (PR #129 review).
 */
export const cookieOptions = () => ({
  httpOnly: true,
  sameSite: "lax" as const,
  secure: secureCookies(),
  path: "/",
});
